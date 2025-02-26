﻿namespace wave.langserver
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Security.Permissions;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.LanguageServer.Protocol;
    internal class FileWatcher
    {
        private readonly ConcurrentBag<System.IO.FileSystemWatcher> watchers;
        private readonly Action<Exception> onException;

        private void OnBufferOverflow(object sender, ErrorEventArgs e)
        {
            // Todo: We should at some point implement a mechanism to try and recover from buffer overflows in the file watcher.
            try
            {
                QsCompilerError.Raise($"buffer overflow in file system watcher: \n{e.GetException()}");
            }
            catch (Exception ex)
            {
                this.onException(ex);
            }
        }

        /// <summary>
        /// the keys contain the *absolute* uri to a folder, and the values are the set of the *relative* names of all contained files and folders
        /// </summary>
        private readonly Dictionary<Uri, ImmutableHashSet<string>> watchedDirectories;
        private readonly ConcurrentDictionary<Uri, IEnumerable<string>> globPatterns;
        private readonly ProcessingQueue processing;

        public event FileEventHandler? FileEvent;

        public delegate void FileEventHandler(FileEvent e);

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public FileWatcher(Action<Exception> onException)
        {
            this.onException = onException;
            this.watchers = new ConcurrentBag<System.IO.FileSystemWatcher>();
            this.watchedDirectories = new Dictionary<Uri, ImmutableHashSet<string>>();
            this.processing = new ProcessingQueue(this.onException, "error in file system watcher");
            this.globPatterns = new ConcurrentDictionary<Uri, IEnumerable<string>>();
        }

        /// <summary>
        /// Returns a file system watcher for the given folder and pattern, with the proper event handlers added.
        /// IMPORTANT: The returned watcher is disabled and needs to be enabled by setting EnableRaisingEvents to true.
        /// </summary>
        private System.IO.FileSystemWatcher GetWatcher(string folder, string pattern, NotifyFilters notifyOn)
        {
            var watcher = new System.IO.FileSystemWatcher
            {
                NotifyFilter = notifyOn,
                Filter = pattern,
                Path = folder,
                // An entry is the buffer is 12 bytes plus the length of the file path times two.
                // The default buffer size is 2*4096, which is around 15 events.
                InternalBufferSize = 16 * 4096,
            };

            watcher.Error += this.OnBufferOverflow;
            watcher.Renamed += this.OnRenamed;
            watcher.Changed += this.OnChanged;
            watcher.Created += this.OnCreated;
            watcher.Deleted += this.OnDeleted;
            return watcher;
        }

        /// <summary>
        /// Initializes the given dictionary with the structure of the given directory as it is currently on disk for the given glob pattern.
        /// Returns true if the routine succeeded without throwing an exception and false otherwise.
        /// Returns true without doing anything if no directory exists at the given (absolute!) path.
        /// </summary>
        private static bool GlobDirectoryStructure(Dictionary<Uri, ImmutableHashSet<string>> directories, string path, IEnumerable<string> globPatterns)
        {
            if (!Directory.Exists(path) || !Uri.TryCreate(path, UriKind.Absolute, out var root))
            {
                return true; // successfully completed, but nothing to be done
            }

            var success = Directory.EnumerateDirectories(root.LocalPath).TryEnumerate(out var subfolders);
            success = globPatterns.TryEnumerate(
                pattern => Directory.EnumerateFiles(root.LocalPath, pattern, SearchOption.TopDirectoryOnly), out var files)
                && success;

            directories[root] = subfolders.Concat(files.SelectMany(items => items)).SelectNotNull(Path.GetFileName).ToImmutableHashSet();
            foreach (var subfolder in subfolders)
            {
                success = GlobDirectoryStructure(directories, subfolder, globPatterns) && success;
            }

            return success;
        }

        /// <summary>
        /// Adds suitable listeners to capture all given glob patterns for the given folder,
        /// and - if subfolders is set to true - all its subfolders.
        /// Does nothing if no folder with the give path exists.
        /// </summary>
        public Task ListenAsync(string? folder, bool subfolders, Action<ImmutableDictionary<Uri, ImmutableHashSet<string>>>? onInitialState, params string[] globPatterns)
        {
            if (folder is null || !Directory.Exists(folder))
            {
                return Task.CompletedTask;
            }
            folder = folder.TrimEnd(Path.DirectorySeparatorChar);

            globPatterns = globPatterns.Distinct().ToArray();
            this.globPatterns.AddOrUpdate(new Uri(folder), globPatterns, (_, currentPatterns) => currentPatterns.Concat(globPatterns).Distinct().ToArray());

            var filters = globPatterns.Select(p => (p, NotifyFilters.FileName | NotifyFilters.LastWrite));
            if (subfolders)
            {
                filters = filters.Concat(new (string, NotifyFilters)[] { (string.Empty, NotifyFilters.DirectoryName) });
            }

            return this.processing.QueueForExecutionAsync(() =>
            {
                var dictionary = new Dictionary<Uri, ImmutableHashSet<string>>();
                if (subfolders && GlobDirectoryStructure(dictionary, folder, globPatterns))
                {
                    foreach (var entry in dictionary)
                    {
                        var current = this.watchedDirectories.TryGetValue(entry.Key, out var c) ? c : ImmutableHashSet<string>.Empty;
                        this.watchedDirectories[entry.Key] = current.Union(entry.Value);
                    }
                    onInitialState?.Invoke(dictionary.ToImmutableDictionary());
                }

                foreach (var (pattern, notifyOn) in filters)
                {
                    if (!this.watchers.Any(watcher =>
                        watcher.Path == folder && watcher.Filter == pattern && watcher.NotifyFilter == notifyOn))
                    {
                        var watcher = this.GetWatcher(folder, pattern, notifyOn);
                        watcher.IncludeSubdirectories = subfolders;
                        watcher.EnableRaisingEvents = true;
                        this.watchers.Add(watcher);
                    }
                }
            });
        }

        // routines called upon creation

        private void OnCreatedFile(string fullPath)
        {
            this.FileEvent?.Invoke(new FileEvent
            {
                Uri = new Uri(fullPath),
                FileChangeType = FileChangeType.Created
            });

            var dir = new Uri(Path.GetDirectoryName(fullPath) ?? "");
            var current = this.watchedDirectories.TryGetValue(dir, out var items) ? items : ImmutableHashSet<string>.Empty;
            this.watchedDirectories[dir] = current.Add(Path.GetFileName(fullPath));
        }

        private void RecurCreated(string fullPath, IDictionary<Uri, ImmutableHashSet<string>> newDirectories)
        {
            var dir = new Uri(fullPath);
            if (newDirectories.TryGetValue(dir, out var items))
            {
                var current = this.watchedDirectories.TryGetValue(dir, out var c) ? c : ImmutableHashSet<string>.Empty;
                this.watchedDirectories[dir] = current.Union(items);
                foreach (var item in items)
                {
                    this.RecurCreated(Path.Combine(fullPath, item), newDirectories);
                }
            }
            else
            {
                this.OnCreatedFile(fullPath);
            }
        }

        // Todo: this routine in particular illustrates the limitations of the current mechanism.
        public void OnCreated(object source, FileSystemEventArgs e)
        {
            var directories = new Dictionary<Uri, ImmutableHashSet<string>>();
            if (source is System.IO.FileSystemWatcher watcher &&
                this.globPatterns.TryGetValue(new Uri(watcher.Path), out var globPatterns))
            {
                var maxNrTries = 10; // copied directories need some time until they are on disk -> todo: better solution?
                while (maxNrTries-- > 0 && !GlobDirectoryStructure(directories, e.FullPath, globPatterns))
                {
                    directories = new Dictionary<Uri, ImmutableHashSet<string>>();
                    System.Threading.Thread.Sleep(1000);
                }
            }
            _ = this.processing.QueueForExecutionAsync(() => this.RecurCreated(e.FullPath, directories));
        }

        // routines called upon deletion

        private void OnDeletedFile(string fullPath)
        {
            this.FileEvent?.Invoke(new FileEvent
            {
                Uri = new Uri(fullPath),
                FileChangeType = FileChangeType.Deleted
            });

            var dir = new Uri(Path.GetDirectoryName(fullPath) ?? "");
            if (this.watchedDirectories.TryGetValue(dir, out var items))
            {
                this.watchedDirectories[dir] = items.Remove(Path.GetFileName(fullPath));
            }
        }

        private void RecurDeleted(string fullPath)
        {
            if (this.watchedDirectories.TryGetValue(new Uri(fullPath), out var items))
            {
                foreach (var item in items)
                {
                    this.RecurDeleted(Path.Combine(fullPath, item));
                }
                this.watchedDirectories.Remove(new Uri(fullPath));
            }
            else
            {
                this.OnDeletedFile(fullPath);
            }
        }

        public void OnDeleted(object source, FileSystemEventArgs e) =>
            this.processing.QueueForExecutionAsync(() => this.RecurDeleted(e.FullPath));

        // routines called upon changing

        private void OnChangedFile(string fullPath) =>
            this.FileEvent?.Invoke(new FileEvent
            {
                Uri = new Uri(fullPath),
                FileChangeType = FileChangeType.Changed
            });

        private void RecurChanged(string fullPath)
        {
            if (this.watchedDirectories.TryGetValue(new Uri(fullPath), out _))
            {
                // nothing to do here
            }
            else
            {
                this.OnChangedFile(fullPath);
            }
        }

        public void OnChanged(object source, FileSystemEventArgs e) =>
            this.processing.QueueForExecutionAsync(() => this.RecurChanged(e.FullPath));

        // routines called upon renaming

        private void OnRenamedFile(string fullPath, string oldFullPath)
        {
            this.OnDeletedFile(oldFullPath);
            this.OnCreatedFile(fullPath);
        }

        private void RecurRenamed(string fullPath, string oldFullPath)
        {
            if (this.watchedDirectories.TryGetValue(new Uri(oldFullPath), out var items))
            {
                this.watchedDirectories[new Uri(fullPath)] = items;
                foreach (var item in items)
                {
                    this.RecurRenamed(Path.Combine(fullPath, item), Path.Combine(oldFullPath, item));
                }
                this.watchedDirectories.Remove(new Uri(oldFullPath));
            }
            else
            {
                this.OnRenamedFile(fullPath, oldFullPath);
            }
        }

        public void OnRenamed(object source, RenamedEventArgs e) =>
            this.processing.QueueForExecutionAsync(() => this.RecurRenamed(e.FullPath, e.OldFullPath));
    }
}
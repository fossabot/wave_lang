﻿namespace wave.syntax
{
    using System.Collections.Generic;
    using System.Linq;

    public class MethodDeclarationSyntax : MemberDeclarationSyntax
    {
        public MethodDeclarationSyntax(MemberDeclarationSyntax heading = null)
            : base(heading)
        {
        }

        public override SyntaxType Kind => SyntaxType.Method;

        public override void Accept(WaveSyntaxVisitor visitor) => visitor.VisitMethodDeclaration(this);

        public override IEnumerable<BaseSyntax> ChildNodes =>
            base.ChildNodes.Concat(GetNodes(ReturnType)).Concat(Parameters).Concat(GetNodes(Body)).Where(n => n != null);

        public TypeSyntax ReturnType { get; set; }

        public string Identifier { get; set; }

        public List<ParameterSyntax> Parameters { get; set; } = new();

        public BlockSyntax Body { get; set; }

        public bool IsAbstract => Body == null;

        public override MemberDeclarationSyntax WithTypeAndName(ParameterSyntax typeAndName)
        {
            ReturnType = typeAndName.Type;
            Identifier = typeAndName.Identifier ?? typeAndName.Type.Identifier;
            return this;
        }
    }
}
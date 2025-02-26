﻿namespace wave.syntax
{
    using System.Collections.Generic;

    public class WhileStatementSyntax : StatementSyntax
    {
        public override SyntaxType Kind => SyntaxType.WhileStatement;

        public override void Accept(WaveSyntaxVisitor visitor) => visitor.VisitWhileStatement(this);

        public override IEnumerable<BaseSyntax> ChildNodes => GetNodes(Expression, Statement);

        public ExpressionSyntax Expression { get; set; }

        public StatementSyntax Statement { get; set; }
    }
}
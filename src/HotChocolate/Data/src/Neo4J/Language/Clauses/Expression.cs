﻿using System;

namespace HotChocolate.Data.Neo4J.Language
{
    /// <summary>
    /// An expression can be used in many places, i.e. in return statements, pattern
    /// elements etc.
    /// </summary>
    public abstract class Expression : Visitable
    {
        public new ClauseKind Kind { get; } = ClauseKind.Expression;
        /// <summary>
        /// Creates an expression with an alias. This expression does not track which or
        /// how many aliases have been created.
        /// </summary>
        /// <param name="alias">The alias to use</param>
        /// <returns>An aliased expression</returns>
        public AliasedExpression As(string alias)
        {
            _ = alias ??
                throw new ArgumentNullException(nameof(alias));
            return new AliasedExpression(this, alias);
        }

        public Condition IsEqualTo(Expression rhs) => Conditions.IsEqualTo(this, rhs);
        public Condition IsNotEqualTo(Expression rhs) => Conditions.IsNotEqualTo(this, rhs);
        public Condition LessThan(Expression rhs) => Conditions.LessThan(this, rhs);
        public Condition LessThanOrEqualTo(Expression rhs) => Conditions.LessThanOrEqualTo(this, rhs);
        public Condition GreaterThan(Expression rhs) => Conditions.GreaterThan(this, rhs);
        public Condition GreaterThanOEqualTo(Expression rhs) => Conditions.GreaterThanOEqualTo(this, rhs);
        public Condition IsTrue() => Conditions.IsEqualTo(this, Cypher.LiteralTrue());
        public Condition IsFalse() => Conditions.IsEqualTo(this, Cypher.LiteralFalse());
        public Condition Matches(Expression expression) => Conditions.Matches(this, expression);
        public Condition Matches(string pattern) => Conditions.Matches(this, Cypher.LiteralOf(pattern));
        public Condition StartsWith(Expression expression) => Conditions.StartsWith(this, expression);
        public Condition EndsWith(Expression expression) => Conditions.EndsWith(this, expression);
        public Condition Contains(Expression expression) => Conditions.Contains(this, expression);
        public Operation Concat(Expression expression) => Operations.Concat(this, expression);
        public Operation Add(Expression expression) => Operations.Add(this, expression);
        public Operation Substract(Expression expression) => Operations.Substract(this, expression);
        public Operation Multiply(Expression expression) => Operations.Multiply(this, expression);
        public Operation Divide(Expression expression) => Operations.Divide(this, expression);
        public Operation Remainder(Expression expression) => Operations.Remainder(this, expression);
        public Operation Pow(Expression expression) => Operations.Pow(this, expression);
        public Condition IsNull() => Conditions.IsNull(this);
        public Condition IsNotNull() => Conditions.IsNotNull(this);
        public Condition In(Expression expression) => Comparison.Create(this, new Operator(Operator.In), expression);
        public Condition IsEmpty() => Conditions.IsEmpty(this);
        public SortItem Descending() => SortItem.Create(this, SortDirection.Desc);
        public SortItem Ascending() => SortItem.Create(this, SortDirection.Asc);
    }
}
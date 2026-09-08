using System.Linq.Expressions;

namespace Quizware.Infrastructure.Persistence;

/// <summary>Combines two lambda predicates over the same entity type into a
/// single ANDed lambda, since each is built with its own parameter.</summary>
internal static class GlobalFilterExpressionCombiner
{
    public static Expression<Func<T, bool>> And<T>(Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(T), "e");
        var body = Expression.AndAlso(
            new ParameterReplacer(left.Parameters[0], parameter).Visit(left.Body),
            new ParameterReplacer(right.Parameters[0], parameter).Visit(right.Body));

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly ParameterExpression _to;

        public ParameterReplacer(ParameterExpression from, ParameterExpression to)
        {
            _from = from;
            _to = to;
        }

        protected override Expression VisitParameter(ParameterExpression node) => node == _from ? _to : node;
    }
}

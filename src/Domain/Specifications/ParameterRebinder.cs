using System.Linq.Expressions;

namespace YAF.Domain.Specifications;

// Replaces one parameter of an expression with another, so that two lambdas can share a single parameter.
internal sealed class ParameterRebinder(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
{
    // Joins the bodies of two lambdas with a binary operator, on the left lambda's parameter.
    public static Expression<Func<T, bool>> Combine<T>(
        Expression<Func<T, bool>> left, Expression<Func<T, bool>> right, Func<Expression, Expression, BinaryExpression> join)
    {
        var parameter = left.Parameters[0];
        var rightBody = new ParameterRebinder(right.Parameters[0], parameter).Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(join(left.Body, rightBody), parameter);
    }

    protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
}

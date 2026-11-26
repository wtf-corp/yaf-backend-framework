using System.Linq.Expressions;

namespace YAF.Domain.Specifications;

// Holds when the operand does not; the operand's parameter stays the only one.
internal sealed class NotSpecification<T>(Specification<T> operand) : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        var expression = operand.ToExpression();
        return Expression.Lambda<Func<T, bool>>(Expression.Not(expression.Body), expression.Parameters);
    }
}

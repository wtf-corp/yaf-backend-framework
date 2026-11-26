using System.Linq.Expressions;

namespace YAF.Domain.Specifications;

// Holds when either operand holds. The right-hand body is rebound onto the left-hand parameter, so the result has one parameter.
internal sealed class OrSpecification<T>(Specification<T> left, Specification<T> right) : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression()
        => ParameterRebinder.Combine(left.ToExpression(), right.ToExpression(), Expression.OrElse);
}

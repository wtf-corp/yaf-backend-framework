using System.Linq.Expressions;
using YAF.Domain.Specifications;

namespace Domain.Tests.Fixtures;

#region specification
internal sealed class PlacedOrders : Specification<IOrderSummary>
{
    public override Expression<Func<IOrderSummary, bool>> ToExpression() => o => o.Status == OrderStatus.Placed;
}
#endregion

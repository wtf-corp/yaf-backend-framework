using System.Linq.Expressions;
using YAF.Domain.Specifications;

namespace Domain.Tests.Fixtures;

internal sealed class OrdersThrough(Channel channel) : Specification<IOrderSummary>
{
    public override Expression<Func<IOrderSummary, bool>> ToExpression() => o => o.Channel == channel;
}

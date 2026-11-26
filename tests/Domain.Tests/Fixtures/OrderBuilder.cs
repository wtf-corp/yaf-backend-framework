using YAF.Domain;

namespace Domain.Tests.Fixtures;

#region builder
internal sealed class OrderBuilder(string title) : AggregateBuilder<Order>
{
    protected override Order Create() => Order.Draft(OrderId.New(), title);

    protected override void OnCreated(Order aggregate) => aggregate.Place();
}
#endregion

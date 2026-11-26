using Domain.Tests.Fixtures;
using YAF.Domain.ValueObjects;
using YAF.Testing;

namespace Domain.Tests.Mementos;

// 02 §12 item 2: the memento round trip with members of every allowed type, their nullable forms, and child add, keep and remove.
public sealed class OrderMementoTests
{
    private static readonly Currency s_euro = Currency.FromCode("EUR").Value;
    private static readonly DateTimeOffset s_monday = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    #region round-trip-test
    [Fact]
    public void RoundTrips_EveryMemberType()
    {
        var order = Order.Create(OrderId.New(), "Books").Value;
        order.AddLine(OrderLineId.New(), 2);
        order.AddLine(OrderLineId.New(), 5);
        order.Amend(
            customerId: CustomerId.New(),
            status: OrderStatus.Placed,
            channel: Channel.Phone,
            discount: Percentage.Create(12.5m).Value,
            contactEmail: EmailAddress.Create("customer-0001@example.test").Value,
            deposit: new Money(20.00m, s_euro),
            deliveryWindow: Period.Create(s_monday, s_monday.AddHours(4)).Value,
            confirmedAt: s_monday.AddDays(-1),
            referredBy: CustomerId.New());

        MementoAssert.RoundTrips(order, () => new OrderData());
    }
    #endregion

    [Fact]
    public void RoundTrips_NullableMembersNull()
    {
        var order = Order.Create(OrderId.New(), "Books").Value;
        order.Amend(
            customerId: CustomerId.New(),
            status: OrderStatus.Draft,
            channel: Channel.Store,
            discount: Percentage.Create(0m).Value,
            contactEmail: null,
            deposit: new Money(0m, s_euro),
            deliveryWindow: null,
            confirmedAt: null,
            referredBy: null);

        MementoAssert.RoundTrips(order, () => new OrderData());
    }

    [Fact]
    public void Snapshot_OntoAUsedMemento_AddsKeepsAndRemovesChildren()
    {
        var order = Order.Create(OrderId.New(), "Books").Value;
        var kept = OrderLineId.New();
        var removed = OrderLineId.New();
        var added = OrderLineId.New();
        order.AddLine(kept, 1);
        order.AddLine(removed, 2);
        var memento = new OrderData();
        order.Snapshot(memento);
        var keptMemento = memento.Lines[0];
        order.RemoveLine(removed);
        order.AddLine(added, 3);

        order.Snapshot(memento);

        memento.Lines.Select(line => line.Id).Should().Equal(kept.Value, added.Value);
        memento.Lines[0].Should().BeSameAs(keptMemento);
        memento.Lines.Select(line => line.Quantity).Should().Equal(1, 3);
    }

    [Fact]
    public void AddLine_ValidLine_KeepsTheInvariants()
    {
        var order = Order.Create(OrderId.New(), "Books").Value;

        var result = order.AddLine(OrderLineId.New(), 2);

        result.ShouldSucceed();
        AggregateAssert.InvariantsHold(order);
    }
}

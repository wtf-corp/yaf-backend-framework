using Domain.Tests.Fixtures;
using YAF.Domain;

namespace Domain.Tests.Aggregates;

public sealed class AggregateRootTests
{
    [Fact]
    public void DomainEvents_TwoRaised_QueuesThemInOrder()
    {
        var order = Order.Create(OrderId.New(), "Books").Value;
        var first = OrderLineId.New();
        var second = OrderLineId.New();

        order.AddLine(first, 1);
        order.AddLine(second, 2);

        order.DomainEvents.Cast<OrderLineAdded>().Select(e => e.LineId).Should().Equal(first, second);
    }

    [Fact]
    public void DomainEvents_FailedBehaviour_QueuesNothing()
    {
        var order = Order.Create(OrderId.New(), "Books").Value;

        var result = order.AddLine(OrderLineId.New(), 0);

        result.Errors.Select(e => e.Code).Should().Equal(OrderErrors.QuantityNotPositive.Code);
        order.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DequeueDomainEvents_ReturnsThenEmptiesQueue()
    {
        var order = Order.Create(OrderId.New(), "Books").Value;
        var first = OrderLineId.New();
        var second = OrderLineId.New();
        order.AddLine(first, 1);
        order.AddLine(second, 2);
        IAggregateRoot root = order;

        var dequeued = root.DequeueDomainEvents();

        dequeued.Cast<OrderLineAdded>().Select(e => e.LineId).Should().Equal(first, second);
        root.DomainEvents.Should().BeEmpty();
        root.DequeueDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public void Version_NewAggregate_IsDefault()
    {
        var order = Order.Create(OrderId.New(), "Books").Value;

        order.Version.Value.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Snapshot_WritesVersion()
    {
        var version = Guid.CreateVersion7();
        var order = Order.Restore(new OrderData { Id = OrderId.New().Value, Version = version, Title = "Books" });
        var memento = new OrderData();

        order.Snapshot(memento);

        memento.Version.Should().Be(version);
    }

    [Fact]
    public void Restore_ReadsVersion()
    {
        var version = Guid.CreateVersion7();

        var order = Order.Restore(new OrderData { Id = OrderId.New().Value, Version = version, Title = "Books" });

        order.Version.Value.Should().Be(version);
    }

    [Fact]
    public void CheckInvariants_ThroughInterface_ReturnsBrokenRule()
    {
        var memento = new OrderData { Id = OrderId.New().Value, Title = "Books" };
        memento.Lines.Add(new OrderLineData { Id = OrderLineId.New().Value, Quantity = 0 });
        IAggregateRoot root = Order.Restore(memento);

        var result = root.CheckInvariants();

        result.Errors.Select(e => e.Code).Should().Equal(OrderErrors.QuantityNotPositive.Code);
    }
}

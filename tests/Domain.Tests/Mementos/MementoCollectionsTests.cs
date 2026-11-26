using Domain.Tests.Fixtures;
using YAF.Domain.Mementos;

namespace Domain.Tests.Mementos;

public sealed class MementoCollectionsTests
{
    [Fact]
    public void Sync_KeptChild_ReusesItsMemento()
    {
        var line = OrderLine.Create(OrderLineId.New(), 1).Value;
        var memento = new OrderLineData();
        line.Snapshot(memento);
        List<OrderLine.IMemento> mementos = [memento];
        line.ChangeQuantity(5);

        MementoCollections.Sync(mementos, [line], () => new OrderLineData());

        mementos.Should().ContainSingle().Which.Should().BeSameAs(memento);
        memento.Quantity.Should().Be(5);
    }

    [Fact]
    public void Sync_NewChild_CreatesMemento()
    {
        var line = OrderLine.Create(OrderLineId.New(), 3).Value;
        List<OrderLine.IMemento> mementos = [];

        MementoCollections.Sync(mementos, [line], () => new OrderLineData());

        var created = mementos.Should().ContainSingle().Which;
        created.Id.Should().Be(line.Id.Value);
        created.Quantity.Should().Be(3);
    }

    [Fact]
    public void Sync_RemovedChild_RemovesMemento()
    {
        var kept = OrderLine.Create(OrderLineId.New(), 1).Value;
        var keptMemento = new OrderLineData();
        kept.Snapshot(keptMemento);
        var removedMemento = new OrderLineData();
        OrderLine.Create(OrderLineId.New(), 2).Value.Snapshot(removedMemento);
        List<OrderLine.IMemento> mementos = [removedMemento, keptMemento];

        MementoCollections.Sync(mementos, [kept], () => new OrderLineData());

        mementos.Should().Equal(keptMemento);
    }

    [Fact]
    public void Sync_ReorderedChildren_MatchesById()
    {
        var first = OrderLine.Create(OrderLineId.New(), 1).Value;
        var second = OrderLine.Create(OrderLineId.New(), 2).Value;
        var firstMemento = new OrderLineData();
        var secondMemento = new OrderLineData();
        first.Snapshot(firstMemento);
        second.Snapshot(secondMemento);
        List<OrderLine.IMemento> mementos = [firstMemento, secondMemento];

        MementoCollections.Sync(mementos, [second, first], () => new OrderLineData());

        mementos.Should().Equal(firstMemento, secondMemento);
        mementos.Select(m => m.Quantity).Should().Equal(1, 2);
    }

    [Fact]
    public void Sync_NewChild_IsAppendedAfterTheKeptOnes()
    {
        var kept = OrderLine.Create(OrderLineId.New(), 1).Value;
        var added = OrderLine.Create(OrderLineId.New(), 2).Value;
        var keptMemento = new OrderLineData();
        kept.Snapshot(keptMemento);
        List<OrderLine.IMemento> mementos = [keptMemento];

        MementoCollections.Sync(mementos, [added, kept], () => new OrderLineData());

        mementos.Select(m => m.Id).Should().Equal(kept.Id.Value, added.Id.Value);
    }
}

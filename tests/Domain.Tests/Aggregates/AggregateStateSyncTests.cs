using Domain.Tests.Fixtures;
using YAF.Domain;

namespace Domain.Tests.Aggregates;

public sealed class AggregateStateSyncTests
{
    [Fact]
    public void SyncVersion_SetsTheAggregatesVersion()
    {
        var order = Order.Create(OrderId.New(), "Books").Value;
        var saved = AggregateVersion.From(Guid.CreateVersion7());
        IAggregateStateSync sync = order;

        sync.SyncVersion(saved);

        order.Version.Should().Be(saved);
    }

    [Fact]
    public void SyncVersion_ThenSnapshot_WritesTheNewVersion()
    {
        var order = Order.Restore(new OrderData { Id = OrderId.New().Value, Version = Guid.CreateVersion7(), Title = "Books" });
        var saved = Guid.CreateVersion7();
        IAggregateStateSync sync = order;
        var memento = new OrderData();

        sync.SyncVersion(AggregateVersion.From(saved));
        order.Snapshot(memento);

        memento.Version.Should().Be(saved);
    }
}

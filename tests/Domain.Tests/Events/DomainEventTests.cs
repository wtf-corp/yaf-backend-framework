using Domain.Tests.Fixtures;

namespace Domain.Tests.Events;

public sealed class DomainEventTests
{
    [Fact]
    public void New_EventId_IsVersion7()
    {
        var placed = new OrderPlaced(OrderId.New());

        placed.EventId.Version.Should().Be(7);
    }

    [Fact]
    public void With_OccurredAt_KeepsTypeAndEventId()
    {
        var placed = new OrderPlaced(OrderId.New());
        var occurredAt = new DateTimeOffset(2026, 1, 1, 9, 30, 0, TimeSpan.Zero);

        var stamped = placed with { OccurredAt = occurredAt };

        stamped.Should().BeOfType<OrderPlaced>();
        stamped.EventId.Should().Be(placed.EventId);
        stamped.OrderId.Should().Be(placed.OrderId);
        stamped.OccurredAt.Should().Be(occurredAt);
    }
}

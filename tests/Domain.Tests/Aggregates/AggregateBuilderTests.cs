using Domain.Tests.Fixtures;
using YAF.Domain;

namespace Domain.Tests.Aggregates;

public sealed class AggregateBuilderTests
{
    [Fact]
    public void Build_InvariantsHold_ReturnsAggregateWithCreatedEvent()
    {
        var builder = new OrderBuilder("Books");

        var result = builder.Build();

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Books");
        result.Value.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OrderPlaced>()
            .Which.OrderId.Should().Be(result.Value.Id);
    }

    [Fact]
    public void Build_InvariantBroken_FailsAndRaisesNothing()
    {
        var builder = new RecordingOrderBuilder(" ");

        var result = builder.Build();

        result.Errors.Select(e => e.Code).Should().Equal(OrderErrors.TitleMissing.Code);
        builder.OnCreatedCalled.Should().BeFalse();
        builder.Created!.DomainEvents.Should().BeEmpty();
    }

    // Records what the builder created, so that the test can look at an aggregate that Build did not return.
    private sealed class RecordingOrderBuilder(string title) : AggregateBuilder<Order>
    {
        public Order? Created { get; private set; }

        public bool OnCreatedCalled { get; private set; }

        protected override Order Create() => Created = Order.Draft(OrderId.New(), title);

        protected override void OnCreated(Order aggregate)
        {
            OnCreatedCalled = true;
            aggregate.Place();
        }
    }
}

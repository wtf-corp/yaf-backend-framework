using YAF.Application.UnitOfWork;
using YAF.Core.Results;
using YAF.Domain;
using YAF.Domain.Events;

namespace Application.Tests.UnitOfWork;

public sealed class AggregateTrackerTests
{
    [Fact]
    public void DequeueDomainEvents_ReturnsThemInTrackingOrder()
    {
        var first = new FakeAggregate(new TalkScheduled(1), new TalkScheduled(2));
        var second = new FakeAggregate(new TalkScheduled(3));
        var tracker = new AggregateTracker();
        tracker.Track(second);
        tracker.Track(first);

        var events = tracker.DequeueDomainEvents();

        events.Should().Equal(second.Raised[0], first.Raised[0], first.Raised[1]);
        tracker.DequeueDomainEvents().Should().BeEmpty();
        tracker.Tracked.Should().Equal(second, first);
    }

    [Fact]
    public void Track_SameAggregateTwice_TracksItOnce()
    {
        var aggregate = new FakeAggregate(new TalkScheduled(1));
        var tracker = new AggregateTracker();

        tracker.Track(aggregate);
        tracker.Track(aggregate);

        tracker.Tracked.Should().ContainSingle().Which.Should().BeSameAs(aggregate);
        tracker.DequeueDomainEvents().Should().ContainSingle();
    }

    [Fact]
    public void Clear_ForgetsEverything()
    {
        var aggregate = new FakeAggregate(new TalkScheduled(1));
        var tracker = new AggregateTracker();
        tracker.Track(aggregate);

        tracker.Clear();

        tracker.Tracked.Should().BeEmpty();
        tracker.DequeueDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public void Track_Null_Throws()
    {
        var tracker = new AggregateTracker();

        var act = () => tracker.Track(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private sealed record TalkScheduled(int Number) : DomainEvent;

    private sealed class FakeAggregate(params IDomainEvent[] raised) : IAggregateRoot
    {
        private readonly List<IDomainEvent> _queue = [.. raised];

        public IDomainEvent[] Raised { get; } = raised;

        public IReadOnlyList<IDomainEvent> DomainEvents => _queue;

        public IReadOnlyList<IDomainEvent> DequeueDomainEvents()
        {
            IDomainEvent[] events = [.. _queue];
            _queue.Clear();
            return events;
        }

        public Result CheckInvariants() => Result.Success();
    }
}

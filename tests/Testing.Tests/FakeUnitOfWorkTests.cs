using Testing.Tests.Fixtures;
using YAF.Application.UnitOfWork;
using YAF.Core.Results;
using YAF.Domain;
using YAF.Domain.Events;
using YAF.Testing;

namespace Testing.Tests;

public sealed class FakeUnitOfWorkTests
{
    private const string Title = "Workshop booking of attendee-0001@example.test";

    private readonly ListTracker _tracker = new();
    private readonly Booking _booking = Booking.Create(BookingId.New(), Title, Guid.CreateVersion7());

    public FakeUnitOfWorkTests()
    {
        _tracker.Track(_booking);
    }

    [Fact]
    public async Task Execute_Success_CountsACommit()
    {
        var unitOfWork = new FakeUnitOfWork();

        var result = await unitOfWork.ExecuteAsync(_ => ValueTask.FromResult(Result.Success(42)), TestContext.Current.CancellationToken);

        result.ShouldSucceed().Should().Be(42);
        unitOfWork.Commits.Should().Be(1);
        unitOfWork.Rollbacks.Should().Be(0);
        unitOfWork.Rounds.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_Failure_CountsARollback()
    {
        var unitOfWork = new FakeUnitOfWork();

        var result = await unitOfWork.ExecuteAsync(_ => ValueTask.FromResult<Result>(BookingErrors.TitleMissing), TestContext.Current.CancellationToken);

        result.ShouldFailWith(BookingErrors.TitleMissing);
        unitOfWork.Commits.Should().Be(0);
        unitOfWork.Rollbacks.Should().Be(1);
    }

    [Fact]
    public async Task Execute_WithTracker_RecordsTheRounds()
    {
        var publisher = new ScriptedPublisher(raised => AddALineWhenConfirmed(_booking, raised));
        var unitOfWork = new FakeUnitOfWork(_tracker, publisher);

        var result = await unitOfWork.ExecuteAsync(_ => ConfirmWithALine(_booking), TestContext.Current.CancellationToken);

        result.ShouldSucceed();
        unitOfWork.Rounds.Should().Equal(2, 1);
        publisher.Calls.Select(call => string.Join(", ", call.Select(raised => raised.GetType().Name))).Should().Equal(
            $"{nameof(BookingConfirmed)}, {nameof(BookingLineAdded)}",
            nameof(BookingLineAdded));
        unitOfWork.Commits.Should().Be(1);
        unitOfWork.Rollbacks.Should().Be(0);
    }

    [Fact]
    public async Task Execute_FailedEventHandler_CountsARollback()
    {
        var unitOfWork = new FakeUnitOfWork(_tracker, new ScriptedPublisher(_ => BookingErrors.QuantityNotPositive));

        var result = await unitOfWork.ExecuteAsync(_ => ConfirmWithValue(_booking), TestContext.Current.CancellationToken);

        result.ShouldFailWith(BookingErrors.QuantityNotPositive);
        unitOfWork.Rounds.Should().Equal(1);
        unitOfWork.Commits.Should().Be(0);
        unitOfWork.Rollbacks.Should().Be(1);
    }

    [Fact]
    public async Task Execute_CascadeBeyondTen_FailsWithDepthCode()
    {
        var unitOfWork = new FakeUnitOfWork(_tracker, new ScriptedPublisher(_ => AddALine(_booking)));

        var result = await unitOfWork.ExecuteAsync(_ => ConfirmWithALine(_booking), TestContext.Current.CancellationToken);

        result.ShouldFailWith(ApplicationErrors.DomainEventDepthExceeded);
        unitOfWork.Rounds.Should().HaveCount(10);
        unitOfWork.Commits.Should().Be(0);
        unitOfWork.Rollbacks.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithoutTrackerOrPublisher_Throws()
    {
        var withoutTracker = () => new FakeUnitOfWork(null!, new ScriptedPublisher(_ => Result.Success()));
        var withoutPublisher = () => new FakeUnitOfWork(_tracker, null!);

        withoutTracker.Should().Throw<ArgumentNullException>().WithParameterName("tracker");
        withoutPublisher.Should().Throw<ArgumentNullException>().WithParameterName("publisher");
    }

    private static ValueTask<Result> ConfirmWithALine(Booking booking)
    {
        booking.Confirm();
        booking.AddLine(BookingLineId.New(), 2);
        return ValueTask.FromResult(Result.Success());
    }

    private static ValueTask<Result<int>> ConfirmWithValue(Booking booking)
    {
        booking.Confirm();
        return ValueTask.FromResult(Result.Success(7));
    }

    private static Result AddALine(Booking booking)
    {
        booking.AddLine(BookingLineId.New(), 1);
        return Result.Success();
    }

    private static Result AddALineWhenConfirmed(Booking booking, IDomainEvent raised) =>
        raised is BookingConfirmed ? AddALine(booking) : Result.Success();

    /// <summary>A hand-written tracker: dequeues the events of the tracked aggregates in tracking order.</summary>
    private sealed class ListTracker : IAggregateTracker
    {
        private readonly List<IAggregateRoot> _tracked = [];

        public IReadOnlyList<IAggregateRoot> Tracked => _tracked;

        public void Track(IAggregateRoot aggregate) => _tracked.Add(aggregate);

        public IReadOnlyList<IDomainEvent> DequeueDomainEvents() => [.. _tracked.SelectMany(aggregate => aggregate.DequeueDomainEvents())];

        public void Clear() => _tracked.Clear();
    }

    /// <summary>A hand-written publisher: runs the reaction for each event until one fails, and records the events of each call.</summary>
    private sealed class ScriptedPublisher(Func<IDomainEvent, Result> react) : IDomainEventPublisher
    {
        public List<IReadOnlyList<IDomainEvent>> Calls { get; } = [];

        public ValueTask<Result> PublishAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct)
        {
            Calls.Add([.. domainEvents]);
            var failed = domainEvents.Select(react).FirstOrDefault(result => result.IsFailure, Result.Success());
            return ValueTask.FromResult(failed);
        }
    }
}

using Testing.Tests.Fixtures;
using YAF.Application.UnitOfWork;
using YAF.Testing;

namespace Testing.Tests;

public sealed class InMemoryRepositoryTests
{
    private const string Title = "Workshop booking of attendee-0001@example.test";

    private readonly RecordingTracker _tracker = new();
    private readonly InMemoryBookingRepository _repository;
    private readonly Booking _booking = NewBooking(BookingId.New());

    public InMemoryRepositoryTests()
    {
        _repository = new InMemoryBookingRepository(_tracker);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddThenFind_ReturnsTheAggregate()
    {
        await _repository.AddAsync(_booking, Ct);

        var found = await _repository.FindAsync(_booking.Id, Ct);

        found.Should().BeSameAs(_booking);
    }

    [Fact]
    public async Task Find_Unknown_ReturnsNull()
    {
        await _repository.AddAsync(_booking, Ct);

        var found = await _repository.FindAsync(BookingId.New(), Ct);

        found.Should().BeNull();
        _tracker.Tracked.Should().Equal(_booking);
    }

    [Fact]
    public async Task Find_Stored_TracksTheAggregate()
    {
        await _repository.AddAsync(_booking, Ct);
        _tracker.Clear();

        await _repository.FindAsync(_booking.Id, Ct);

        _tracker.Tracked.Should().Equal(_booking);
    }

    [Fact]
    public async Task Add_TracksTheAggregate()
    {
        await _repository.AddAsync(_booking, Ct);

        _tracker.Tracked.Should().Equal(_booking);
    }

    [Fact]
    public async Task Add_StoredId_Throws()
    {
        await _repository.AddAsync(_booking, Ct);

        var act = () => _repository.AddAsync(NewBooking(_booking.Id), Ct).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>();
        _repository.Stored.Should().Equal(_booking);
    }

    [Fact]
    public async Task Stored_HasEveryAddedAggregate()
    {
        var other = NewBooking(BookingId.New());

        await _repository.AddAsync(_booking, Ct);
        await _repository.AddAsync(other, Ct);

        _repository.Stored.Should().HaveCount(2).And.Contain([_booking, other]);
    }

    [Fact]
    public async Task Update_TracksTheAggregate()
    {
        await _repository.AddAsync(_booking, Ct);
        _tracker.Clear();

        await _repository.UpdateAsync(_booking, Ct);

        _tracker.Tracked.Should().Equal(_booking);
    }

    [Fact]
    public async Task Update_AnotherInstance_ReplacesTheStoredOne()
    {
        var restored = NewBooking(_booking.Id);
        await _repository.AddAsync(_booking, Ct);

        await _repository.UpdateAsync(restored, Ct);

        var found = await _repository.FindAsync(_booking.Id, Ct);
        found.Should().BeSameAs(restored);
    }

    [Fact]
    public async Task Update_Unknown_Throws()
    {
        var act = () => _repository.UpdateAsync(_booking, Ct).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>();
        _repository.Stored.Should().BeEmpty();
        _tracker.Tracked.Should().BeEmpty();
    }

    [Fact]
    public async Task Remove_ForgetsIt()
    {
        await _repository.AddAsync(_booking, Ct);

        await _repository.RemoveAsync(_booking, Ct);

        var found = await _repository.FindAsync(_booking.Id, Ct);
        found.Should().BeNull();
        _repository.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Remove_TracksTheAggregate()
    {
        await _repository.AddAsync(_booking, Ct);
        _tracker.Clear();

        await _repository.RemoveAsync(_booking, Ct);

        _tracker.Tracked.Should().Equal(_booking);
    }

    [Fact]
    public async Task Remove_Unknown_Throws()
    {
        var act = () => _repository.RemoveAsync(_booking, Ct).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>();
        _tracker.Tracked.Should().BeEmpty();
    }

    [Fact]
    public async Task Members_NullAggregate_Throw()
    {
        var add = () => _repository.AddAsync(null!, Ct).AsTask();
        var update = () => _repository.UpdateAsync(null!, Ct).AsTask();
        var remove = () => _repository.RemoveAsync(null!, Ct).AsTask();

        (await add.Should().ThrowAsync<ArgumentNullException>()).WithParameterName("aggregate");
        (await update.Should().ThrowAsync<ArgumentNullException>()).WithParameterName("aggregate");
        (await remove.Should().ThrowAsync<ArgumentNullException>()).WithParameterName("aggregate");
    }

    [Fact]
    public void Constructor_WithoutTrackerOrIdOf_Throws()
    {
        var withoutTracker = () => new InMemoryBookingRepository(null!);
        var withoutIdOf = () => new WithoutIdOf(_tracker);

        withoutTracker.Should().Throw<ArgumentNullException>().WithParameterName("tracker");
        withoutIdOf.Should().Throw<ArgumentNullException>().WithParameterName("idOf");
    }

    private static Booking NewBooking(BookingId id) => Booking.Create(id, Title, Guid.CreateVersion7());

    private sealed class WithoutIdOf(IAggregateTracker tracker) : InMemoryRepository<Booking, BookingId>(tracker, null!);
}

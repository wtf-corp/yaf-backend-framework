using YAF.Application.UnitOfWork;
using YAF.Testing;

namespace Testing.Tests.Fixtures;

/// <summary>The in-memory repository of the fixture aggregate, as an application test derives one for its own repository interface.</summary>
internal sealed class InMemoryBookingRepository(IAggregateTracker tracker)
    : InMemoryRepository<Booking, BookingId>(tracker, booking => booking.Id);

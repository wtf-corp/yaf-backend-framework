using YAF.Domain.Events;

namespace Testing.Tests.Fixtures;

internal sealed record BookingLineAdded(BookingId BookingId, BookingLineId LineId) : DomainEvent;

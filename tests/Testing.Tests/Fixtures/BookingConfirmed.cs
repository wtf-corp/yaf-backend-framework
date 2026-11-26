using YAF.Domain.Events;

namespace Testing.Tests.Fixtures;

internal sealed record BookingConfirmed(BookingId BookingId) : DomainEvent;

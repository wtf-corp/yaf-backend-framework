using YAF.Domain.Events;

namespace Domain.Tests.Fixtures;

internal sealed record OrderLineAdded(OrderId OrderId, OrderLineId LineId, int Quantity) : DomainEvent;

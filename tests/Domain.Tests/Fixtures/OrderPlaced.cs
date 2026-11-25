using YAF.Domain.Events;

namespace Domain.Tests.Fixtures;

#region domain-event
internal sealed record OrderPlaced(OrderId OrderId) : DomainEvent;
#endregion

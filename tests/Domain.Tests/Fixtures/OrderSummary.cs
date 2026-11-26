using YAF.Domain.ValueObjects;

namespace Domain.Tests.Fixtures;

internal sealed record OrderSummary(OrderId Id, OrderStatus Status, Channel Channel, Money Total) : IOrderSummary;

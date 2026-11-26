using YAF.Domain.ValueObjects;

namespace Domain.Tests.Fixtures;

// The typed members a specification is written against (02 §9): a typed id, an enum, a smart enumeration and a value object.
internal interface IOrderSummary
{
    OrderId Id { get; }

    OrderStatus Status { get; }

    Channel Channel { get; }

    Money Total { get; }
}

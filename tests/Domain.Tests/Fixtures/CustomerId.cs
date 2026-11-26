using YAF.Core.Ids;

namespace Domain.Tests.Fixtures;

// The typed id of another aggregate, which an order holds by id only.
[TypedId]
internal readonly partial record struct CustomerId;

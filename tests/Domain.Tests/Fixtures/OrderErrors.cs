using YAF.Core.Results;

namespace Domain.Tests.Fixtures;

internal abstract class OrderErrors : IErrorCatalog
{
    public static readonly ErrorDescriptor QuantityNotPositive = Error.Validation("Quantity must be positive.");

    public static readonly ErrorDescriptor QuantityAboveLimit = Error.Validation("Quantity must not exceed the limit.");
}

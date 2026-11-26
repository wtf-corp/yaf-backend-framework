using YAF.Core.Results;

namespace Testing.Tests.Fixtures;

internal abstract class BookingErrors : IErrorCatalog
{
    public static readonly ErrorDescriptor TitleMissing = Error.Validation("A booking needs a title.");

    public static readonly ErrorDescriptor QuantityNotPositive = Error.Validation("Quantity must be positive.");
}

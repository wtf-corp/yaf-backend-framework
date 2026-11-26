using YAF.Core.Results;

namespace YAF.Domain.ValueObjects;

/// <summary>The errors that the value objects of <c>YAF.Domain</c> report.</summary>
public sealed class ValueObjectErrors : IErrorCatalog
{
    /// <summary>
    /// The error of <see cref="Currency.FromCode"/> for a code that is not in the table: code <c>core.currency.unknown</c>, kind
    /// <see cref="ErrorKind.Validation"/>. Its argument is <c>Code</c>, the code given.
    /// </summary>
    [ErrorCode("core.currency.unknown")]
    public static readonly ErrorDescriptor UnknownCurrency = Error.Validation("'{Code}' is not an ISO 4217 currency code.");

    private ValueObjectErrors()
    {
    }
}

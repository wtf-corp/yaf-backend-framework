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

    /// <summary>
    /// The error of <see cref="Money.Add"/> and <see cref="Money.Subtract"/> for two amounts in different currencies: code
    /// <c>core.money.currency_mismatch</c>, kind <see cref="ErrorKind.Validation"/>. Its arguments are <c>Left</c> and <c>Right</c>, the
    /// codes of the two currencies.
    /// </summary>
    [ErrorCode("core.money.currency_mismatch")]
    public static readonly ErrorDescriptor CurrencyMismatch = Error.Validation("Amounts in {Left} and {Right} cannot be combined.");

    /// <summary>
    /// The error of <see cref="Percentage.Create"/> for a value below 0 or above 100: code <c>core.percentage.out_of_range</c>, kind
    /// <see cref="ErrorKind.Validation"/>. It has no arguments.
    /// </summary>
    [ErrorCode("core.percentage.out_of_range")]
    public static readonly ErrorDescriptor PercentageOutOfRange = Error.Validation("A percentage is between 0 and 100.");

    /// <summary>
    /// The error of <see cref="DateRange.Create"/> for a last day before the first: code <c>core.date_range.end_before_start</c>, kind
    /// <see cref="ErrorKind.Validation"/>. It has no arguments.
    /// </summary>
    [ErrorCode("core.date_range.end_before_start")]
    public static readonly ErrorDescriptor DateRangeEndBeforeStart = Error.Validation("A date range cannot end before it starts.");

    /// <summary>
    /// The error of <see cref="Period.Create"/> for an end before the start: code <c>core.period.end_before_start</c>, kind
    /// <see cref="ErrorKind.Validation"/>. It has no arguments.
    /// </summary>
    [ErrorCode("core.period.end_before_start")]
    public static readonly ErrorDescriptor PeriodEndBeforeStart = Error.Validation("A period cannot end before it starts.");

    private ValueObjectErrors()
    {
    }
}

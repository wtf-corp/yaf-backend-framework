using YAF.Core.Results;

namespace YAF.Domain.ValueObjects;

/// <summary>
/// A percentage from 0 to 100 inclusive, applied to an amount with <see cref="Of(Money)"/> or <see cref="Of(decimal)"/>. Equal percentages
/// have the same <see cref="Value"/>, whatever its scale (12.5 and 12.50).
/// </summary>
/// <remarks>
/// There is no public constructor: create a percentage with <see cref="Create"/>. Applying it does not round; call
/// <see cref="Money.Round"/> on the result to round it to the minor units.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var euro = Currency.FromCode("EUR").Value;
/// Money discount = Percentage.Create(12.5m).Value.Of(new Money(80.00m, euro)); // 10 EUR
/// </code>
/// </example>
public sealed record Percentage : ValueObject
{
    private Percentage(decimal value) => Value = value;

    /// <summary>Gets the percentage, from 0 to 100 inclusive (12.5 is twelve and a half per cent).</summary>
    public decimal Value { get; }

    /// <summary>Creates a percentage.</summary>
    /// <param name="value">The percentage, for example as read from the store or a request.</param>
    /// <returns>
    /// The percentage; or <see cref="ValueObjectErrors.PercentageOutOfRange"/> when <paramref name="value"/> is below 0 or above 100.
    /// </returns>
    public static Result<Percentage> Create(decimal value) =>
        value is >= 0m and <= 100m ? new Percentage(value) : ValueObjectErrors.PercentageOutOfRange;

    /// <summary>Applies the percentage to an amount of money, without rounding.</summary>
    /// <param name="money">The amount.</param>
    /// <returns>The share of <paramref name="money"/>, in the same currency.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="money"/> is <see langword="null"/>.</exception>
    public Money Of(Money money)
    {
        ArgumentNullException.ThrowIfNull(money);
        return new Money(Of(money.Amount), money.Currency);
    }

    /// <summary>Applies the percentage to a number, without rounding.</summary>
    /// <param name="amount">The number.</param>
    /// <returns>The share of <paramref name="amount"/>.</returns>
    public decimal Of(decimal amount) => amount * (Value / 100m);
}

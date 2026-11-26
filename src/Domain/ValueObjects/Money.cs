using YAF.Core.Results;

namespace YAF.Domain.ValueObjects;

/// <summary>
/// An amount of money in one <see cref="ValueObjects.Currency"/>. Arithmetic refuses to combine two currencies, rounding names its mode, and
/// <see cref="Allocate(ReadOnlySpan{int})"/> splits an amount without losing a minor unit.
/// </summary>
/// <remarks>
/// Any amount is valid, negative ones and ones finer than the currency's minor units included: <see cref="Multiply"/> does not round, and
/// <see cref="Round"/> is the one member that does. Equal amounts in the same currency are equal, whatever their scale (1.5 and 1.50).
/// </remarks>
/// <example>
/// <code language="csharp">
/// var euro = Currency.FromCode("EUR").Value;
/// IReadOnlyList&lt;Money&gt; shares = new Money(100.00m, euro).Allocate(3); // 33.34, 33.33, 33.33
/// </code>
/// </example>
/// <param name="Amount">The amount, in units of the currency (2.50 is two euros and fifty cents).</param>
/// <param name="Currency">The currency of the amount.</param>
/// <exception cref="ArgumentNullException"><paramref name="Currency"/> is <see langword="null"/>.</exception>
public sealed record Money(decimal Amount, Currency Currency) : ValueObject
{
    /// <summary>Gets the amount, in units of the currency.</summary>
    public decimal Amount { get; } = Amount;

    /// <summary>Gets the currency of the amount.</summary>
    public Currency Currency { get; } = Currency ?? throw new ArgumentNullException(nameof(Currency));

    /// <summary>Adds an amount in the same currency.</summary>
    /// <param name="other">The amount to add.</param>
    /// <returns>
    /// The sum; or <see cref="ValueObjectErrors.CurrencyMismatch"/>, with the arguments <c>Left</c> and <c>Right</c>, when
    /// <paramref name="other"/> is in another currency.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public Result<Money> Add(Money other) => Combine(other, 1);

    /// <summary>Subtracts an amount in the same currency.</summary>
    /// <param name="other">The amount to subtract.</param>
    /// <returns>
    /// The difference; or <see cref="ValueObjectErrors.CurrencyMismatch"/>, with the arguments <c>Left</c> and <c>Right</c>, when
    /// <paramref name="other"/> is in another currency.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public Result<Money> Subtract(Money other) => Combine(other, -1);

    /// <summary>Multiplies the amount by a factor, without rounding.</summary>
    /// <param name="factor">The factor.</param>
    /// <returns>The product, in the same currency; call <see cref="Round"/> to round it to the minor units.</returns>
    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    /// <summary>Rounds the amount to the minor units of its currency.</summary>
    /// <param name="mode">How a value halfway between two minor units is rounded.</param>
    /// <returns>The rounded amount, in the same currency.</returns>
    public Money Round(MidpointRounding mode) => new(decimal.Round(Amount, Currency.MinorUnits, mode), Currency);

    /// <summary>Splits the amount into equal shares that add up to it exactly.</summary>
    /// <param name="parts">The number of shares, 1 or more.</param>
    /// <returns>The shares, as <see cref="Allocate(ReadOnlySpan{int})"/> with <paramref name="parts"/> ratios of 1 computes them.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parts"/> is less than 1.</exception>
    /// <exception cref="OverflowException">
    /// The amount in minor units is too large to be multiplied by a ratio within the range of <see cref="decimal"/>.
    /// </exception>
    public IReadOnlyList<Money> Allocate(int parts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(parts, 1);
        var ratios = new int[parts];
        Array.Fill(ratios, 1);
        return Allocate(ratios);
    }

    /// <summary>Splits the amount by ratios into shares that add up to it exactly.</summary>
    /// <remarks>
    /// Each share is the amount times its ratio over the sum of the ratios, truncated toward zero to a minor unit. What is left is handed out
    /// one minor unit at a time to the shares in order, skipping those with a ratio of 0; a rest finer than a minor unit goes to the first
    /// share with a ratio above 0. A currency without minor units is split into whole units.
    /// </remarks>
    /// <param name="ratios">One ratio per share, none negative, at least one above 0.</param>
    /// <returns>The shares, in the order of <paramref name="ratios"/>, in the same currency.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="ratios"/> is empty, holds a negative ratio, or its ratios add up to 0.
    /// </exception>
    /// <exception cref="OverflowException">
    /// The amount in minor units times a ratio leaves the range of <see cref="decimal"/>, even when the share itself would fit: for example
    /// an amount of 10^18 units with two decimals and a ratio of <see cref="int.MaxValue"/>.
    /// </exception>
    public IReadOnlyList<Money> Allocate(params ReadOnlySpan<int> ratios)
    {
        long sum = 0;
        foreach (var ratio in ratios)
        {
            if (ratio < 0)
            {
                throw new ArgumentException("A ratio cannot be negative.", nameof(ratios));
            }

            sum += ratio;
        }

        if (sum == 0)
        {
            throw new ArgumentException("At least one ratio must be above 0.", nameof(ratios));
        }

        var scale = Pow10(Currency.MinorUnits);
        var total = Amount * scale;
        var shares = new decimal[ratios.Length];
        var rest = total;
        for (var i = 0; i < ratios.Length; i++)
        {
            shares[i] = decimal.Truncate(total * ratios[i] / sum);
            rest -= shares[i];
        }

        var unit = Math.Sign(total);
        for (var i = 0; Math.Abs(rest) >= 1m; i++)
        {
            if (ratios[i] > 0)
            {
                shares[i] += unit;
                rest -= unit;
            }
        }

        shares[ratios.IndexOfAnyExcept(0)] += rest;
        return Array.ConvertAll(shares, share => new Money(share / scale, Currency));
    }

    private static decimal Pow10(int exponent)
    {
        var result = 1m;
        for (var i = 0; i < exponent; i++)
        {
            result *= 10m;
        }

        return result;
    }

    private Result<Money> Combine(Money other, int sign)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other.Currency == Currency
            ? new Money(Amount + (sign * other.Amount), Currency)
            : ValueObjectErrors.CurrencyMismatch.With(("Left", Currency.Code), ("Right", other.Currency.Code));
    }
}

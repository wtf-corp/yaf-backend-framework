using System.Collections.Frozen;
using System.Collections.ObjectModel;
using YAF.Core.Results;

namespace YAF.Domain.ValueObjects;

/// <summary>
/// An ISO 4217 currency: its alphabetic <see cref="Code"/> and the number of <see cref="MinorUnits"/> an amount in it is rounded to.
/// Equal currencies have the same code.
/// </summary>
/// <remarks>
/// The currencies are those of ISO 4217 list one that have a numeric minor unit; codes without one (precious metals, units of account,
/// <c>XTS</c>, <c>XXX</c>) are not money amounts and are not listed. There is no public constructor: get a currency from
/// <see cref="FromCode"/> or <see cref="All"/>.
/// </remarks>
/// <example>
/// <code language="csharp">
/// Result&lt;Currency&gt; euro = Currency.FromCode("EUR"); // MinorUnits is 2
/// </code>
/// </example>
public sealed record Currency : ValueObject
{
    // ISO 4217 list one, published 2026-09-17 (Pblshd), fetched 2026-11-26 from
    // https://www.six-group.com/dam/download/financial-information/data-center/iso-currrency/lists/list-one.xml.
    // Each entry is the alphabetic code followed by its minor units, in ordinal order of the code; entries with "N.A." are left out.
    private const string Table =
        "AED2AFN2ALL2AMD2AOA2ARS2AUD2AWG2AZN2BAM2BBD2BDT2BHD3BIF0BMD2BND2BOB2BOV2BRL2BSD2BTN2BWP2BYN2BZD2CAD2CDF2CHE2CHF2" +
        "CHW2CLF4CLP0CNY2COP2COU2CRC2CUP2CVE2CZK2DJF0DKK2DOP2DZD2EGP2ERN2ETB2EUR2FJD2FKP2GBP2GEL2GHS2GIP2GMD2GNF0GTQ2GYD2" +
        "HKD2HNL2HTG2HUF2IDR2ILS2INR2IQD3IRR2ISK0JMD2JOD3JPY0KES2KGS2KHR2KMF0KPW2KRW0KWD3KYD2KZT2LAK2LBP2LKR2LRD2LSL2LYD3" +
        "MAD2MDL2MGA2MKD2MMK2MNT2MOP2MRU2MUR2MVR2MWK2MXN2MXV2MYR2MZN2NAD2NGN2NIO2NOK2NPR2NZD2OMR3PAB2PEN2PGK2PHP2PKR2PLN2" +
        "PYG0QAR2RON2RSD2RUB2RWF0SAR2SBD2SCR2SDG2SEK2SGD2SHP2SLE2SOS2SRD2SSP2STN2SVC2SYP2SZL2THB2TJS2TMT2TND3TOP2TRY2TTD2" +
        "TWD2TZS2UAH2UGX0USD2USN2UYI0UYU2UYW4UZS2VED2VES2VND0VUV0WST2XAD2XAF0XCD2XCG2XOF0XPF0YER2ZAR2ZMW2ZWG2";

    private static readonly ReadOnlyCollection<Currency> s_all = ReadTable();
    private static readonly FrozenDictionary<string, Currency> s_byCode = s_all.ToFrozenDictionary(currency => currency.Code, StringComparer.Ordinal);

    private Currency(string code, int minorUnits)
    {
        Code = code;
        MinorUnits = minorUnits;
    }

    /// <summary>Gets the alphabetic ISO 4217 code, three upper-case letters, for example <c>EUR</c>.</summary>
    public string Code { get; }

    /// <summary>Gets the number of digits after the decimal separator of an amount in this currency: 0, 2, 3 or 4.</summary>
    public int MinorUnits { get; }

    /// <summary>Gets every currency once, in ordinal order of the code.</summary>
    public static IReadOnlyList<Currency> All => s_all;

    /// <summary>Finds the currency with an alphabetic ISO 4217 code, compared ordinally (upper case only).</summary>
    /// <param name="code">The code, for example as read from the store or a request.</param>
    /// <returns>
    /// The currency; or <see cref="ValueObjectErrors.UnknownCurrency"/>, with the argument <c>Code</c>, when no currency has
    /// <paramref name="code"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is <see langword="null"/>.</exception>
    public static Result<Currency> FromCode(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return s_byCode.TryGetValue(code, out var currency)
            ? currency
            : ValueObjectErrors.UnknownCurrency.With(("Code", code));
    }

    private static ReadOnlyCollection<Currency> ReadTable() =>
        Enumerable.Range(0, Table.Length / 4)
            .Select(i => new Currency(Table.Substring(i * 4, 3), Table[(i * 4) + 3] - '0'))
            .ToArray()
            .AsReadOnly();
}

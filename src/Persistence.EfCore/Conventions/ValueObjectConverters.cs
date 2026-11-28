using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using YAF.Core.Results;
using YAF.Domain.Mementos;
using YAF.Domain.ValueObjects;

namespace YAF.Persistence.EfCore.Conventions;

/// <summary>Stores a <see cref="Currency"/> as its ISO 4217 code.</summary>
internal sealed class CurrencyConverter() : ValueConverter<Currency, string>(
    currency => currency.Code, code => ValueObjectConverters.Restore(Currency.FromCode(code)));

/// <summary>Stores an <see cref="EmailAddress"/> as its value.</summary>
internal sealed class EmailAddressConverter() : ValueConverter<EmailAddress, string>(
    address => address.Value, value => ValueObjectConverters.Restore(EmailAddress.Create(value)));

/// <summary>Stores a <see cref="Percentage"/> as its value.</summary>
internal sealed class PercentageConverter() : ValueConverter<Percentage, decimal>(
    percentage => percentage.Value, value => ValueObjectConverters.Restore(Percentage.Create(value)));

internal static class ValueObjectConverters
{
    // The value object the factory made from the stored value, or a CorruptStateException when it made none.
    public static T Restore<T>(Result<T> restored) => restored.IsSuccess
        ? restored.Value
        : throw new CorruptStateException($"A stored {typeof(T).Name} is not valid: {restored.FirstError.Code}.");
}

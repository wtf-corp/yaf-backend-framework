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

/// <summary>What the value-object converters share.</summary>
internal static class ValueObjectConverters
{
    /// <summary>Returns the restored value object, or throws when the stored value no longer makes one.</summary>
    /// <param name="restored">The result of the value object's factory over the stored value.</param>
    /// <typeparam name="T">The value object.</typeparam>
    /// <returns>The value object.</returns>
    public static T Restore<T>(Result<T> restored) => restored.IsSuccess
        ? restored.Value
        : throw new CorruptStateException($"A stored {typeof(T).Name} is not valid: {restored.FirstError.Code}.");
}

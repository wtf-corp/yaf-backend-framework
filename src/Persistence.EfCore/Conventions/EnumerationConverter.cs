using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using YAF.Domain;
using YAF.Domain.Mementos;

namespace YAF.Persistence.EfCore.Conventions;

/// <summary>Stores a smart enumeration as its <see cref="Enumeration{TSelf}.Code"/>.</summary>
/// <typeparam name="TEnumeration">The smart enumeration.</typeparam>
/// <remarks>
/// A <see cref="YafDbContext"/> applies it by convention to every smart enumeration member of its data classes; a service does not
/// configure it. Reading a code that no instance has throws a <see cref="CorruptStateException"/>.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var stored = new EnumerationConverter&lt;SessionFormat&gt;().ConvertToProvider(SessionFormat.Workshop);
/// </code>
/// </example>
public sealed class EnumerationConverter<TEnumeration> : ValueConverter<TEnumeration, string>
    where TEnumeration : Enumeration<TEnumeration>
{
    /// <summary>Initializes a new instance.</summary>
    public EnumerationConverter()
        : base(enumeration => enumeration.Code, code => Restore(code))
    {
    }

    private static TEnumeration Restore(string code)
    {
        var result = Enumeration<TEnumeration>.FromCode(code);
        return result.IsSuccess
            ? result.Value
            : throw new CorruptStateException($"A stored {typeof(TEnumeration).Name} has a code that no instance has.");
    }
}

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YAF.Domain;

/// <summary>
/// Converts every smart enumeration (a type <c>T</c> that derives from <see cref="Enumeration{TSelf}"/> of <c>T</c>) to and from a JSON
/// string holding its <see cref="Enumeration{TSelf}.Code"/>.
/// </summary>
/// <remarks>
/// <see cref="System.Text.Json"/> does not read a converter attribute from a base type, so <see cref="Enumeration{TSelf}"/> cannot attach
/// this factory for its derived types: add it to <see cref="JsonSerializerOptions.Converters"/>, or name it on the type.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var options = new JsonSerializerOptions { Converters = { new EnumerationJsonConverterFactory() } };
///
/// [JsonConverter(typeof(EnumerationJsonConverterFactory))]
/// public sealed record SessionFormat : Enumeration&lt;SessionFormat&gt; { … }
/// </code>
/// </example>
public sealed class EnumerationJsonConverterFactory : JsonConverterFactory
{
    private static readonly MethodInfo s_create =
        typeof(EnumerationJsonConverterFactory).GetMethod(nameof(Create), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Tells whether a type is a smart enumeration this factory converts.</summary>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <returns><see langword="true"/> when <paramref name="typeToConvert"/> derives from <see cref="Enumeration{TSelf}"/> of itself.</returns>
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        for (var type = typeToConvert.BaseType; type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Enumeration<>))
            {
                return type.GetGenericArguments()[0] == typeToConvert;
            }
        }

        return false;
    }

    /// <summary>Creates the converter for a smart enumeration.</summary>
    /// <param name="typeToConvert">A type for which <see cref="CanConvert"/> returns <see langword="true"/>.</param>
    /// <param name="options">The serializer options; not used.</param>
    /// <returns>A converter that writes the code and reads it through <see cref="Enumeration{TSelf}.FromCode"/>; it throws
    /// <see cref="JsonException"/> for a code no instance has.</returns>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        return (JsonConverter)s_create.MakeGenericMethod(typeToConvert).Invoke(null, null)!;
    }

    private static EnumerationJsonConverter<TSelf> Create<TSelf>()
        where TSelf : Enumeration<TSelf> => new();
}

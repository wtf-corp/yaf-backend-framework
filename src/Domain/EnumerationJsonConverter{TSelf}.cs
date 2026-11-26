using System.Text.Json;
using System.Text.Json.Serialization;

namespace YAF.Domain;

/// <summary>Converts one smart enumeration to and from a JSON string holding its code.</summary>
/// <typeparam name="TSelf">The smart enumeration.</typeparam>
internal sealed class EnumerationJsonConverter<TSelf> : JsonConverter<TSelf>
    where TSelf : Enumeration<TSelf>
{
    /// <inheritdoc/>
    public override TSelf Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // The message names the type, never the value read (11 §3).
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"A {typeof(TSelf).Name} is written as a JSON string holding its code.");
        }

        var result = Enumeration<TSelf>.FromCode(reader.GetString()!);
        return result.IsSuccess ? result.Value : throw new JsonException($"The JSON string is not a code of {typeof(TSelf).Name}.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options) => writer.WriteStringValue(value.Code);
}

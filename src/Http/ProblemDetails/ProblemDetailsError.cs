using System.Text.Json.Serialization;

namespace YAF.Http;

/// <summary>One entry of the <c>errors</c> array of a problem document. The names are fixed, whatever naming policy the host's JSON uses.</summary>
internal sealed record ProblemDetailsError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("target")] string? Target,
    [property: JsonPropertyName("arguments")] IReadOnlyDictionary<string, object?> Arguments);

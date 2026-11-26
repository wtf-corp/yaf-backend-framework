using System.Collections.ObjectModel;
using System.Reflection;
using YAF.Core.Results;

namespace YAF.Domain;

/// <summary>
/// The base of a smart enumeration: a closed set of instances that carry data or behaviour, each with a stable <see cref="Code"/> that is
/// stored and serialized. Use a C# <see langword="enum"/> when the values carry nothing.
/// </summary>
/// <remarks>
/// The instances are the public static fields of type <typeparamref name="TSelf"/> declared on <typeparamref name="TSelf"/>. For JSON,
/// register <see cref="EnumerationJsonConverterFactory"/> in the serializer options or name it on the type with
/// <c>[JsonConverter(typeof(EnumerationJsonConverterFactory))]</c>; a converter on this base type would not be used for the derived type.
/// </remarks>
/// <example>
/// <code language="csharp">
/// public sealed record SessionFormat : Enumeration&lt;SessionFormat&gt;
/// {
///     public static readonly SessionFormat Talk = new("talk", "Talk", maxSpeakers: 2);
///     public static readonly SessionFormat Workshop = new("workshop", "Workshop", maxSpeakers: 4);
///
///     private SessionFormat(string code, string name, int maxSpeakers) : base(code, name) => MaxSpeakers = maxSpeakers;
///
///     public int MaxSpeakers { get; }
/// }
/// </code>
/// </example>
/// <typeparam name="TSelf">The derived type itself.</typeparam>
public abstract record Enumeration<TSelf>
    where TSelf : Enumeration<TSelf>
{
    private static readonly Lazy<ReadOnlyCollection<TSelf>> s_all = new(ReadFields);

    /// <summary>Initializes a new instance; call it only from the initializer of a public static field of <typeparamref name="TSelf"/>.</summary>
    /// <param name="code">The stable code, stored and serialized. Never change it once released.</param>
    /// <param name="name">The display name.</param>
    /// <exception cref="ArgumentException"><paramref name="code"/> or <paramref name="name"/> is <see langword="null"/>, empty or white
    /// space.</exception>
    protected Enumeration(string code, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Code = code;
        Name = name;
    }

    /// <summary>Gets the stable code, which is stored and serialized.</summary>
    public string Code { get; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; }

    /// <summary>Gets every instance, in the order its field is declared on <typeparamref name="TSelf"/>.</summary>
    public static IReadOnlyList<TSelf> All => s_all.Value;

    /// <summary>Finds the instance with a code, compared ordinally (case-sensitive).</summary>
    /// <param name="code">The code, for example as read from the store or a request.</param>
    /// <returns>
    /// The instance; or <see cref="DomainErrors.UnknownEnumerationCode"/>, with the arguments <c>Code</c> and <c>Enumeration</c>, when no
    /// instance has <paramref name="code"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is <see langword="null"/>.</exception>
    public static Result<TSelf> FromCode(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        foreach (var instance in All)
        {
            if (string.Equals(instance.Code, code, StringComparison.Ordinal))
            {
                return instance;
            }
        }

        return DomainErrors.UnknownEnumerationCode.With(("Code", code), ("Enumeration", typeof(TSelf).Name));
    }

    private static ReadOnlyCollection<TSelf> ReadFields() =>
        typeof(TSelf).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(field => field.FieldType == typeof(TSelf))
            .OrderBy(field => field.MetadataToken)
            .Select(field => (TSelf)field.GetValue(null)!)
            .ToArray()
            .AsReadOnly();
}

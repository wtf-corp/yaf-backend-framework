using System.Text;
using YAF.Core.Classification;
using YAF.Core.Results;

namespace YAF.Domain.ValueObjects;

/// <summary>
/// An email address, checked for its syntax only: one <c>@</c>, a local part of 1 to 64 octets, a domain that is not empty, at most
/// 254 octets in all (UTF-8, the limits of RFC 5321), and no white space or control character. The domain is stored in lower case; the
/// local part is kept as given.
/// </summary>
/// <remarks>
/// There is no public constructor: create an address with <see cref="Create"/>. The type is classified as personal contact data, and
/// that classification applies to every member that holds it. <see cref="ToString"/> returns <c>***</c>, so that an interpolated or
/// logged address does not reveal it: read the address from <see cref="Value"/>. Whether the address exists or receives mail is not
/// checked.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var email = EmailAddress.Create("Ada@Example.ORG").Value; // Value is "Ada@example.org"
/// </code>
/// </example>
[PersonalData(PersonalDataCategory.Contact)]
public sealed record EmailAddress : ValueObject
{
    private const int MaxLocalPartOctets = 64;
    private const int MaxOctets = 254;

    private EmailAddress(string value) => Value = value;

    /// <summary>Gets the address, with its domain in lower case.</summary>
    public string Value { get; }

    /// <summary>Creates an email address.</summary>
    /// <param name="value">The address, for example as read from the store or a request.</param>
    /// <returns>
    /// The address, with its domain in lower case; or <see cref="ValueObjectErrors.InvalidEmailAddress"/> when <paramref name="value"/>
    /// does not have exactly one <c>@</c>, has an empty local part or domain, a local part above 64 octets, more than 254 octets in all,
    /// or a white space or control character.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static Result<EmailAddress> Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0
            || at == value.Length - 1
            || value.IndexOf('@', at + 1, StringComparison.Ordinal) >= 0
            || Encoding.UTF8.GetByteCount(value.AsSpan(0, at)) > MaxLocalPartOctets
            || Encoding.UTF8.GetByteCount(value) > MaxOctets
            || value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return ValueObjectErrors.InvalidEmailAddress;
        }

        return new EmailAddress(string.Concat(value.AsSpan(0, at + 1), LowerAscii(value.AsSpan(at + 1))));
    }

    /// <summary>Returns <c>***</c>, never the address, so that it does not end up in a log or a message.</summary>
    /// <returns><c>***</c>.</returns>
    public override string ToString() => "***";

    // Lowers the ASCII letters only. ToLowerInvariant would depend on Unicode case rules (CA1308).
    private static string LowerAscii(ReadOnlySpan<char> domain) =>
        string.Create(domain.Length, domain, static (target, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                target[i] = source[i] is >= 'A' and <= 'Z' ? (char)(source[i] + ('a' - 'A')) : source[i];
            }
        });
}

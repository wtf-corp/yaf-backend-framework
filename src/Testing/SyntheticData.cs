using System.Globalization;

namespace YAF.Testing;

/// <summary>
/// Personal data for tests that is obviously synthetic: names and e-mail addresses made of a role and an index padded with zeros to four
/// digits. Every address is on <c>example.test</c>, under the top-level domain <c>.test</c> that RFC 2606 reserves for testing, so no
/// address reaches a real mailbox. The same index and role always give the same value.
/// </summary>
/// <remarks>Use it wherever a test needs a person's name or address; never copy real personal data into a test.</remarks>
/// <example>
/// <code>
/// var email = SyntheticData.Email(1);           // attendee-0001@example.test
/// var name = SyntheticData.Name(2, "Speaker");  // Speaker 0002
/// </code>
/// </example>
public static class SyntheticData
{
    /// <summary>Returns the e-mail address of the synthetic person <paramref name="index"/> of <paramref name="role"/>.</summary>
    /// <param name="index">Tells the persons of one role apart; zero or more, padded with zeros to at least four digits.</param>
    /// <param name="role">The start of the local part, such as <c>attendee</c> or <c>speaker</c>.</param>
    /// <returns>The address <c>&lt;role&gt;-&lt;index&gt;@example.test</c>, such as <c>attendee-0001@example.test</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    public static string Email(int index, string role = "attendee") =>
        string.Create(CultureInfo.InvariantCulture, $"{role}-{Padded(index)}@example.test");

    /// <summary>Returns the name of the synthetic person <paramref name="index"/> of <paramref name="role"/>.</summary>
    /// <param name="index">Tells the persons of one role apart; zero or more, padded with zeros to at least four digits.</param>
    /// <param name="role">The first word of the name, such as <c>Attendee</c> or <c>Speaker</c>.</param>
    /// <returns>The name <c>&lt;role&gt; &lt;index&gt;</c>, such as <c>Attendee 0001</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    public static string Name(int index, string role = "Attendee") =>
        string.Create(CultureInfo.InvariantCulture, $"{role} {Padded(index)}");

    private static string Padded(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return index.ToString("D4", CultureInfo.InvariantCulture);
    }
}

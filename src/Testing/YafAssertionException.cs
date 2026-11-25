namespace YAF.Testing;

/// <summary>
/// The exception every helper of <c>YAF.Testing</c> throws when an assertion fails. Any test framework reports it as a failed test, so the
/// helpers need no test framework or assertion library.
/// </summary>
/// <remarks>
/// A message names what was expected and what was found by error codes, type names and ids, never by error messages or values, which may
/// contain personal data.
/// </remarks>
public sealed class YafAssertionException : Exception
{
    /// <summary>Initializes a new instance with a default message.</summary>
    public YafAssertionException()
    {
    }

    /// <summary>Initializes a new instance with <paramref name="message"/>.</summary>
    /// <param name="message">What was expected and what was found, with codes and names and no values.</param>
    public YafAssertionException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with <paramref name="message"/> and the exception that caused it.</summary>
    /// <param name="message">What was expected and what was found, with codes and names and no values.</param>
    /// <param name="innerException">The exception that caused it.</param>
    public YafAssertionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

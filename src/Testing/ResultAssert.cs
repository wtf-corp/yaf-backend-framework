using YAF.Core.Results;

namespace YAF.Testing;

/// <summary>
/// Assertions on <see cref="Result"/> and <see cref="Result{T}"/> that compare error <b>codes</b>, never messages. A failed assertion
/// throws <see cref="YafAssertionException"/>, whose message lists the codes it found and never an error message or argument.
/// </summary>
/// <example>
/// <code>
/// var result = session.Reserve(seat);
///
/// result.ShouldFailWith(Session.Errors.Full);
/// </code>
/// </example>
public static class ResultAssert
{
    /// <summary>Asserts that <paramref name="result"/> is a success.</summary>
    /// <param name="result">The result to check.</param>
    /// <exception cref="YafAssertionException">The result is a failure; the message lists its error codes.</exception>
    public static void ShouldSucceed(this Result result)
    {
        if (result.IsFailure)
        {
            throw FailedInsteadOfSucceeded(result.Errors);
        }
    }

    /// <summary>Asserts that <paramref name="result"/> is a success and returns its value.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result to check.</param>
    /// <returns>The value of the successful result, for further assertions.</returns>
    /// <exception cref="YafAssertionException">The result is a failure; the message lists its error codes.</exception>
    public static T ShouldSucceed<T>(this Result<T> result)
    {
        if (result.IsFailure)
        {
            throw FailedInsteadOfSucceeded(result.Errors);
        }

        return result.Value;
    }

    /// <summary>Asserts that <paramref name="result"/> is a failure and that one of its errors has the code of <paramref name="expected"/>.</summary>
    /// <param name="result">The result to check.</param>
    /// <param name="expected">The descriptor whose code is expected, usually a field of the error catalogue under test.</param>
    /// <exception cref="ArgumentNullException"><paramref name="expected"/> is <see langword="null"/>.</exception>
    /// <exception cref="YafAssertionException">
    /// The result is a success, or none of its errors has the expected code; the message lists the codes found.
    /// </exception>
    public static void ShouldFailWith(this Result result, ErrorDescriptor expected) =>
        CheckFailedWith(result.IsSuccess, result.Errors, expected);

    /// <summary>Asserts that <paramref name="result"/> is a failure and that one of its errors has the code of <paramref name="expected"/>.</summary>
    /// <typeparam name="T">The type of the value the result would carry on success.</typeparam>
    /// <param name="result">The result to check.</param>
    /// <param name="expected">The descriptor whose code is expected, usually a field of the error catalogue under test.</param>
    /// <exception cref="ArgumentNullException"><paramref name="expected"/> is <see langword="null"/>.</exception>
    /// <exception cref="YafAssertionException">
    /// The result is a success, or none of its errors has the expected code; the message lists the codes found.
    /// </exception>
    public static void ShouldFailWith<T>(this Result<T> result, ErrorDescriptor expected) =>
        CheckFailedWith(result.IsSuccess, result.Errors, expected);

    private static void CheckFailedWith(bool isSuccess, IReadOnlyList<Error> errors, ErrorDescriptor expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        if (isSuccess)
        {
            throw new YafAssertionException($"Expected a failure with the error code '{expected.Code}', but the result is a success.");
        }

        if (!errors.Any(error => string.Equals(error.Code, expected.Code, StringComparison.Ordinal)))
        {
            throw new YafAssertionException(
                $"Expected a failure with the error code '{expected.Code}', but the result failed with the error codes {ListCodes(errors)}.");
        }
    }

    private static YafAssertionException FailedInsteadOfSucceeded(IReadOnlyList<Error> errors) =>
        new($"Expected a success, but the result failed with the error codes {ListCodes(errors)}.");

    private static string ListCodes(IReadOnlyList<Error> errors) => string.Join(", ", errors.Select(error => $"'{error.Code}'"));
}

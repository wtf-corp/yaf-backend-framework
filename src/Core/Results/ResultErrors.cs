using System.Collections.ObjectModel;

namespace YAF.Core.Results;

/// <summary>The error lists that <see cref="Result"/> and <see cref="Result{T}"/> share.</summary>
internal static class ResultErrors
{
    /// <summary>Gets the errors of a <c>default</c> result.</summary>
    public static ReadOnlyCollection<Error> Uninitialised { get; } = new([CoreErrors.Uninitialised.ToError()]);

    /// <summary>The message of the exception thrown when the first error of a success is read.</summary>
    public const string NoErrorMessage = "The result is a success and has no error. Check IsFailure first.";

    /// <summary>Copies the errors of a failure into a read-only list.</summary>
    /// <param name="errors">The errors; at least one, none <see langword="null"/>.</param>
    /// <returns>The copied errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty or contains <see langword="null"/>.</exception>
    public static ReadOnlyCollection<Error> Copy(IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var copy = errors.ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException("A failure needs at least one error.", nameof(errors));
        }

        if (Array.IndexOf(copy, null) >= 0)
        {
            throw new ArgumentException("A failure cannot carry a null error.", nameof(errors));
        }

        return Array.AsReadOnly(copy);
    }

    /// <summary>Puts the one error of a failure into a read-only list.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The list with <paramref name="error"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static ReadOnlyCollection<Error> Single(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new([error]);
    }
}

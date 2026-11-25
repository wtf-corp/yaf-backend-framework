using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace YAF.Core;

/// <summary>
/// Argument checks for programmer errors: each one throws an exception of the <see cref="ArgumentException"/> family that names
/// the argument. Business outcomes are never guarded; they are returned as a failed <see cref="Results.Result"/>.
/// </summary>
/// <example>
/// <code language="csharp">
/// public Session(SessionId id, string title)
/// {
///     Guard.NotDefault(id);
///     Guard.NotNullOrWhiteSpace(title);
///     ...
/// }
/// </code>
/// </example>
public static class Guard
{
    private const string DefaultValueMessage = "The value must not be the default of its type.";

    /// <summary>Checks that <paramref name="value"/> is not <see langword="null"/>.</summary>
    /// <param name="value">The argument to check.</param>
    /// <param name="paramName">The argument's name; filled in by the compiler from the expression passed as <paramref name="value"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static void NotNull([NotNull] object? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
    }

    /// <summary>Checks that <paramref name="value"/> is neither <see langword="null"/>, empty, nor only white space.</summary>
    /// <param name="value">The argument to check.</param>
    /// <param name="paramName">The argument's name; filled in by the compiler from the expression passed as <paramref name="value"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or consists only of white space.</exception>
    public static void NotNullOrWhiteSpace([NotNull] string? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
    }

    /// <summary>Checks that <paramref name="value"/> is not the default of its type, for example <c>default(SessionId)</c>.</summary>
    /// <typeparam name="T">The value type of the argument.</typeparam>
    /// <param name="value">The argument to check.</param>
    /// <param name="paramName">The argument's name; filled in by the compiler from the expression passed as <paramref name="value"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> equals <c>default(T)</c>.</exception>
    public static void NotDefault<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : struct
    {
        if (EqualityComparer<T>.Default.Equals(value, default))
        {
            throw new ArgumentException(DefaultValueMessage, paramName);
        }
    }
}

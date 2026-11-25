namespace YAF.Core;

/// <summary>
/// A value that a caller may or may not have supplied, for patch-style requests: it tells "not supplied" apart from
/// "supplied as <see langword="null"/>". <c>default</c> means not supplied; any value of <typeparamref name="T"/>, including
/// <see langword="null"/>, converts implicitly to a supplied optional.
/// </summary>
/// <typeparam name="T">The type of the value, nullable when <see langword="null"/> is a meaningful input.</typeparam>
/// <example>
/// <code language="csharp">
/// public sealed record ChangeSession(SessionId Id, Optional&lt;string&gt; Title, Optional&lt;string?&gt; Abstract);
///
/// if (command.Abstract.HasValue)
/// {
///     session.ChangeAbstract(command.Abstract.Value); // may be null: the caller cleared it
/// }
/// </code>
/// </example>
public readonly struct Optional<T> : IEquatable<Optional<T>>
{
    private readonly T _value;

    private Optional(T value)
    {
        _value = value;
        HasValue = true;
    }

    /// <summary>Gets whether the caller supplied a value. <see langword="false"/> for <c>default</c>.</summary>
    public bool HasValue { get; }

    /// <summary>Gets the supplied value, which may be <see langword="null"/>. Check <see cref="HasValue"/> first.</summary>
    /// <exception cref="InvalidOperationException">No value was supplied (<see cref="HasValue"/> is <see langword="false"/>).</exception>
    public T Value => HasValue ? _value : throw new InvalidOperationException("No value was supplied.");

    /// <summary>Wraps <paramref name="value"/>, which may be <see langword="null"/>, as a supplied optional.</summary>
    /// <param name="value">The supplied value.</param>
    public static implicit operator Optional<T>(T value) => new(value);

    /// <summary>Compares two optionals: both not supplied, or both supplied with equal values.</summary>
    /// <param name="left">The first optional.</param>
    /// <param name="right">The second optional.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> equals <paramref name="right"/>.</returns>
    public static bool operator ==(Optional<T> left, Optional<T> right) => left.Equals(right);

    /// <summary>Compares two optionals: one supplied and the other not, or both supplied with different values.</summary>
    /// <param name="left">The first optional.</param>
    /// <param name="right">The second optional.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> does not equal <paramref name="right"/>.</returns>
    public static bool operator !=(Optional<T> left, Optional<T> right) => !left.Equals(right);

    /// <summary>
    /// Tells whether <paramref name="other"/> is equal: both not supplied, or both supplied with values equal by
    /// <see cref="EqualityComparer{T}.Default"/>. A supplied <see langword="null"/> is not equal to "not supplied".
    /// </summary>
    /// <param name="other">The optional to compare with.</param>
    /// <returns><see langword="true"/> when the two are equal.</returns>
    public bool Equals(Optional<T> other) =>
        HasValue == other.HasValue && EqualityComparer<T>.Default.Equals(_value, other._value);

    /// <summary>Tells whether <paramref name="obj"/> is an <see cref="Optional{T}"/> equal to this one (see <see cref="Equals(Optional{T})"/>).</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal <see cref="Optional{T}"/>.</returns>
    public override bool Equals(object? obj) => obj is Optional<T> other && Equals(other);

    /// <summary>Returns a hash code consistent with <see cref="Equals(Optional{T})"/>.</summary>
    /// <returns>The hash code of <see cref="HasValue"/> and the value.</returns>
    public override int GetHashCode() => HashCode.Combine(HasValue, _value);
}

using System.Collections.ObjectModel;

namespace YAF.Core.Results;

/// <summary>
/// The outcome of an operation that returns a value: a success with a <typeparamref name="T"/>, or a failure with one or more errors.
/// Business outcomes are results, never exceptions.
/// </summary>
/// <remarks>
/// <c>default(Result&lt;T&gt;)</c> is a failure with the error of <see cref="CoreErrors.Uninitialised"/>, never a silent success. Return a
/// value for a success, or <see cref="Result.Success{T}(T)"/> when <typeparamref name="T"/> is an interface type, from which C# allows no
/// user-defined conversion. Return an <see cref="Error"/>, an <see cref="ErrorDescriptor"/> or an array or list of errors for a failure.
/// There is no conversion back to <typeparamref name="T"/>: read <see cref="Value"/> after checking <see cref="IsSuccess"/>.
/// </remarks>
/// <example>
/// <code language="csharp">
/// public Result&lt;SeatId&gt; ReserveSeat(RegistrationId registration)
/// {
///     if (_seats.Count == Capacity) return Errors.Full.With(("Capacity", Capacity));
///     var seat = Seat.Reserve(registration);
///     _seats.Add(seat);
///     return seat.Id;
/// }
/// </code>
/// </example>
/// <typeparam name="T">The type of the value of a success.</typeparam>
public readonly struct Result<T> : IResult
{
    private readonly T _value;
    private readonly ReadOnlyCollection<Error>? _errors;
    private readonly bool _isSuccess;

    private Result(T value)
    {
        _value = value;
        _errors = null;
        _isSuccess = true;
    }

    private Result(ReadOnlyCollection<Error> errors)
    {
        _value = default!;
        _errors = errors;
        _isSuccess = false;
    }

    /// <inheritdoc/>
    public bool IsSuccess => _isSuccess;

    /// <summary>Gets a value indicating whether the result is a failure. A <c>default</c> result is a failure.</summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>Gets the value of a success.</summary>
    /// <exception cref="InvalidOperationException">The result is a failure; the exception message names the first error code.</exception>
    public T Value => _isSuccess
        ? _value
        : throw new InvalidOperationException($"The result is a failure ({FirstError.Code}) and has no value. Check IsSuccess first.");

    /// <inheritdoc/>
    public IReadOnlyList<Error> Errors => _isSuccess ? ReadOnlyCollection<Error>.Empty : _errors ?? ResultErrors.Uninitialised;

    /// <summary>Gets the first error of a failure.</summary>
    /// <exception cref="InvalidOperationException">The result is a success.</exception>
    public Error FirstError => _isSuccess ? throw new InvalidOperationException(ResultErrors.NoErrorMessage) : Errors[0];

    /// <summary>Converts a value to a success. C# applies no user-defined conversion from an interface type: use <see cref="Result.Success{T}(T)"/>.</summary>
    /// <param name="value">The value.</param>
    public static implicit operator Result<T>(T value) => new(value);

    /// <summary>Converts an error to a failure.</summary>
    /// <param name="error">The error.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static implicit operator Result<T>(Error error) => new(ResultErrors.Single(error));

    /// <summary>Converts a declared error to a failure with the error of <see cref="ErrorDescriptor.ToError"/>.</summary>
    /// <param name="descriptor">The declared error.</param>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is <see langword="null"/>.</exception>
    public static implicit operator Result<T>(ErrorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return new(ResultErrors.Single(descriptor.ToError()));
    }

    /// <summary>Converts errors to a failure, for example to pass on the errors of another result: <c>return other.Errors.ToArray();</c>.</summary>
    /// <param name="errors">The errors, at least one. They are copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty or contains <see langword="null"/>.</exception>
    public static implicit operator Result<T>(Error[] errors) => new(ResultErrors.Copy(errors));

    /// <summary>Converts errors to a failure.</summary>
    /// <param name="errors">The errors, at least one. They are copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty or contains <see langword="null"/>.</exception>
    public static implicit operator Result<T>(List<Error> errors) => new(ResultErrors.Copy(errors));

    /// <summary>Creates a success with a value. Works for every <typeparamref name="T"/>, also an interface type.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The successful result.</returns>
    public static Result<T> Success(T value) => new(value);

    /// <summary>Creates a failure with one error.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The failed result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static Result<T> Failure(Error error) => new(ResultErrors.Single(error));

    /// <summary>Creates a failure with the errors in the given order.</summary>
    /// <param name="errors">The errors, at least one. They are copied.</param>
    /// <returns>The failed result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty or contains <see langword="null"/>.</exception>
    public static Result<T> Failure(IEnumerable<Error> errors) => new(ResultErrors.Copy(errors));
}

using System.Collections.ObjectModel;

namespace YAF.Core.Results;

/// <summary>
/// The outcome of an operation that returns no value: a success, or a failure with one or more errors. Business outcomes are results,
/// never exceptions.
/// </summary>
/// <remarks>
/// <c>default(Result)</c> is a failure with the error of <see cref="CoreErrors.Uninitialised"/>, never a silent success. Create a success
/// with <see cref="Success()"/>; return an <see cref="Error"/>, an <see cref="ErrorDescriptor"/> or an array or list of errors for a failure.
/// </remarks>
/// <example>
/// <code language="csharp">
/// public Result Close()
/// {
///     if (IsClosed) return Errors.AlreadyClosed;
///     IsClosed = true;
///     return Result.Success();
/// }
/// </code>
/// </example>
public readonly struct Result : IResult
{
    private readonly ReadOnlyCollection<Error>? _errors;
    private readonly bool _isSuccess;

    private Result(bool isSuccess, ReadOnlyCollection<Error>? errors)
    {
        _isSuccess = isSuccess;
        _errors = errors;
    }

    /// <inheritdoc/>
    public bool IsSuccess => _isSuccess;

    /// <summary>Gets a value indicating whether the result is a failure. A <c>default</c> result is a failure.</summary>
    public bool IsFailure => !_isSuccess;

    /// <inheritdoc/>
    public IReadOnlyList<Error> Errors => _isSuccess ? ReadOnlyCollection<Error>.Empty : _errors ?? ResultErrors.Uninitialised;

    /// <summary>Gets the first error of a failure.</summary>
    /// <exception cref="InvalidOperationException">The result is a success.</exception>
    public Error FirstError => _isSuccess ? throw new InvalidOperationException(ResultErrors.NoErrorMessage) : Errors[0];

    /// <summary>Converts an error to a failure.</summary>
    /// <param name="error">The error.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static implicit operator Result(Error error) => Failure(error);

    /// <summary>Converts a declared error to a failure with the error of <see cref="ErrorDescriptor.ToError"/>.</summary>
    /// <param name="descriptor">The declared error.</param>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is <see langword="null"/>.</exception>
    public static implicit operator Result(ErrorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return Failure(descriptor.ToError());
    }

    /// <summary>Converts errors to a failure, for example to pass on the errors of another result: <c>return other.Errors.ToArray();</c>.</summary>
    /// <param name="errors">The errors, at least one. They are copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty or contains <see langword="null"/>.</exception>
    public static implicit operator Result(Error[] errors) => Failure(errors);

    /// <summary>Converts errors to a failure.</summary>
    /// <param name="errors">The errors, at least one. They are copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty or contains <see langword="null"/>.</exception>
    public static implicit operator Result(List<Error> errors) => Failure(errors);

    /// <summary>Creates a success.</summary>
    /// <returns>The successful result.</returns>
    public static Result Success() => new(isSuccess: true, errors: null);

    /// <summary>
    /// Creates a success with a value. Works for every <typeparamref name="T"/>, also an interface type, from which C# allows no
    /// user-defined conversion.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The successful result.</returns>
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    /// <summary>Creates a failure with one error.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The failed result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static Result Failure(Error error) => new(isSuccess: false, ResultErrors.Single(error));

    /// <summary>Creates a failure with the errors in the given order.</summary>
    /// <param name="errors">The errors, at least one. They are copied.</param>
    /// <returns>The failed result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty or contains <see langword="null"/>.</exception>
    public static Result Failure(IEnumerable<Error> errors) => new(isSuccess: false, ResultErrors.Copy(errors));

    /// <summary>Runs an action after a success. A failure keeps its errors and <paramref name="action"/> is not called.</summary>
    /// <param name="action">The action, called on a success.</param>
    /// <returns>This result: a success after <paramref name="action"/> has run, or the failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public Result Then(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_isSuccess)
        {
            action();
        }

        return this;
    }

    /// <summary>Continues a success with an operation that can fail. A failure keeps its errors and <paramref name="continuation"/> is not called.</summary>
    /// <param name="continuation">The operation, called on a success.</param>
    /// <returns>The result of <paramref name="continuation"/>, or this failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="continuation"/> is <see langword="null"/>.</exception>
    public Result Then(Func<Result> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        return _isSuccess ? continuation() : this;
    }

    /// <summary>Continues a success with an operation that returns a value and can fail. A failure keeps its errors and <paramref name="continuation"/> is not called.</summary>
    /// <typeparam name="T">The type of the value of the operation.</typeparam>
    /// <param name="continuation">The operation, called on a success.</param>
    /// <returns>The result of <paramref name="continuation"/>, or a failure with the same errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="continuation"/> is <see langword="null"/>.</exception>
    public Result<T> Then<T>(Func<Result<T>> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        return _isSuccess ? continuation() : new Result<T>(_errors ?? ResultErrors.Uninitialised);
    }
}

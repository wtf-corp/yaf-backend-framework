using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

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
/// <para>
/// The struct is a union of its value and its errors (<see cref="IUnionMembers"/>), so a <see langword="switch"/> with a
/// <typeparamref name="T"/> arm and an <see cref="IReadOnlyList{T}"/> of <see cref="Error"/> arm is exhaustive and does not box.
/// A <c>default</c> result takes the error arm. The union also brings implicit conversions from an interface
/// <typeparamref name="T"/> and from an <see cref="IReadOnlyList{T}"/> of <see cref="Error"/>; for
/// <c>Result&lt;IReadOnlyList&lt;Error&gt;&gt;</c> a conversion from that list is ambiguous: use <see cref="Success(T)"/> or <see cref="Failure(IEnumerable{Error})"/>.
/// </para>
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
/// <code language="csharp">
/// var message = reservation switch
/// {
///     SeatId seat => $"Reserved {seat}.",
///     IReadOnlyList&lt;Error&gt; errors => $"Failed with {errors[0].Code}.",
/// };
/// </code>
/// </example>
/// <typeparam name="T">The type of the value of a success.</typeparam>
[Union]
public readonly struct Result<T> : IResult, Result<T>.IUnionMembers, IFailureFactory
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

    internal Result(ReadOnlyCollection<Error> errors)
    {
        _value = default!;
        _errors = errors;
        _isSuccess = false;
    }

    /// <summary>
    /// The members through which the compiler treats <see cref="Result{T}"/> as a union of its value and its errors. Callers use them
    /// through patterns and conversions; implement nothing.
    /// </summary>
    public interface IUnionMembers
    {
        /// <summary>Gets the case of the result: the value of a success, or the errors of a failure. A <c>default</c> result gives the errors of <see cref="CoreErrors.Uninitialised"/>.</summary>
        object? Value { get; }

        /// <summary>Gets a value indicating whether the result has a case. Always <see langword="true"/>: a <c>default</c> result is the error case.</summary>
        bool HasValue { get; }

        /// <summary>Creates a success with a value.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The successful result.</returns>
        static abstract Result<T> Create(T value);

        /// <summary>Creates a failure with the errors in the given order.</summary>
        /// <param name="errors">The errors, at least one. They are copied.</param>
        /// <returns>The failed result.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="errors"/> is empty or contains <see langword="null"/>.</exception>
        static abstract Result<T> Create(IReadOnlyList<Error> errors);

        /// <summary>Gets the value of a success without boxing it.</summary>
        /// <param name="value">The value of a success; otherwise the default of <typeparamref name="T"/>.</param>
        /// <returns><see langword="true"/> when the result is a success.</returns>
        bool TryGetValue(out T value);

        /// <summary>Gets the errors of a failure.</summary>
        /// <param name="errors">The errors of a failure, at least one; otherwise an empty list.</param>
        /// <returns><see langword="true"/> when the result is a failure, also a <c>default</c> one.</returns>
        bool TryGetValue(out IReadOnlyList<Error> errors);
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

    /// <summary>Returns the value of one of two functions: <paramref name="onSuccess"/> with the value, or <paramref name="onFailure"/> with the errors.</summary>
    /// <typeparam name="TOut">The type of the returned value.</typeparam>
    /// <param name="onSuccess">Called with the value of a success.</param>
    /// <param name="onFailure">Called with the errors of a failure, at least one.</param>
    /// <returns>What the called function returns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> or <paramref name="onFailure"/> is <see langword="null"/>.</exception>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<IReadOnlyList<Error>, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return _isSuccess ? onSuccess(_value) : onFailure(Errors);
    }

    /// <summary>Runs one of two actions: <paramref name="onSuccess"/> with the value, or <paramref name="onFailure"/> with the errors.</summary>
    /// <param name="onSuccess">Called with the value of a success.</param>
    /// <param name="onFailure">Called with the errors of a failure, at least one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> or <paramref name="onFailure"/> is <see langword="null"/>.</exception>
    public void Switch(Action<T> onSuccess, Action<IReadOnlyList<Error>> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        if (_isSuccess)
        {
            onSuccess(_value);
        }
        else
        {
            onFailure(Errors);
        }
    }

    /// <summary>Transforms the value of a success. A failure keeps its errors and <paramref name="selector"/> is not called.</summary>
    /// <typeparam name="TOut">The type of the new value.</typeparam>
    /// <param name="selector">Computes the new value from the value of a success.</param>
    /// <returns>A success with the new value, or a failure with the same errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is <see langword="null"/>.</exception>
    public Result<TOut> Map<TOut>(Func<T, TOut> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return _isSuccess ? new Result<TOut>(selector(_value)) : new Result<TOut>(FailureErrors);
    }

    /// <summary>Continues a success with an operation that can fail. A failure keeps its errors and <paramref name="continuation"/> is not called.</summary>
    /// <typeparam name="TOut">The type of the value of the operation.</typeparam>
    /// <param name="continuation">The operation, called with the value of a success.</param>
    /// <returns>The result of <paramref name="continuation"/>, or a failure with the same errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="continuation"/> is <see langword="null"/>.</exception>
    public Result<TOut> Then<TOut>(Func<T, Result<TOut>> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        return _isSuccess ? continuation(_value) : new Result<TOut>(FailureErrors);
    }

    /// <summary>Turns a success into a failure when its value breaks a rule. A failure keeps its errors and <paramref name="predicate"/> is not called.</summary>
    /// <param name="predicate">The rule, called with the value of a success; <see langword="true"/> when the value satisfies it.</param>
    /// <param name="error">The declared error of the failure when <paramref name="predicate"/> returns <see langword="false"/>.</param>
    /// <returns>This result, or a failure with the error of <see cref="ErrorDescriptor.ToError"/> when the value breaks the rule.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> or <paramref name="error"/> is <see langword="null"/>.</exception>
    public Result<T> Ensure(Func<T, bool> predicate, ErrorDescriptor error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);
        return !_isSuccess || predicate(_value) ? this : new Result<T>(ResultErrors.Single(error.ToError()));
    }

    /// <summary>Runs an action with the value of a success, for example to log or record it. A failure keeps its errors and <paramref name="action"/> is not called.</summary>
    /// <param name="action">The action, called with the value of a success.</param>
    /// <returns>This result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public Result<T> Tap(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_isSuccess)
        {
            action(_value);
        }

        return this;
    }

    /// <summary>Replaces a failure with a fallback value computed from its errors. A success keeps its value and <paramref name="fallback"/> is not called.</summary>
    /// <param name="fallback">Computes the value from the errors of a failure, at least one.</param>
    /// <returns>This success, or a success with the value of <paramref name="fallback"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fallback"/> is <see langword="null"/>.</exception>
    public Result<T> Else(Func<IReadOnlyList<Error>, T> fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        return _isSuccess ? this : new Result<T>(fallback(FailureErrors));
    }

    /// <summary>Returns the value of one of two asynchronous functions: <paramref name="onSuccess"/> with the value, or <paramref name="onFailure"/> with the errors. An <see langword="async"/> lambda picks this overload.</summary>
    /// <typeparam name="TOut">The type of the returned value.</typeparam>
    /// <param name="onSuccess">Called with the value of a success.</param>
    /// <param name="onFailure">Called with the errors of a failure, at least one.</param>
    /// <returns>The task of the called function.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> or <paramref name="onFailure"/> is <see langword="null"/>; thrown before any function is called.</exception>
    [OverloadResolutionPriority(1)]
    public Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess, Func<IReadOnlyList<Error>, Task<TOut>> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return _isSuccess ? onSuccess(_value) : onFailure(Errors);
    }

    /// <summary>Returns the value of one of two functions that return a <see cref="ValueTask{TResult}"/>: <paramref name="onSuccess"/> with the value, or <paramref name="onFailure"/> with the errors.</summary>
    /// <typeparam name="TOut">The type of the returned value.</typeparam>
    /// <param name="onSuccess">Called with the value of a success.</param>
    /// <param name="onFailure">Called with the errors of a failure, at least one.</param>
    /// <returns>The value task of the called function.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> or <paramref name="onFailure"/> is <see langword="null"/>; thrown before any function is called.</exception>
    public ValueTask<TOut> MatchAsync<TOut>(Func<T, ValueTask<TOut>> onSuccess, Func<IReadOnlyList<Error>, ValueTask<TOut>> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return _isSuccess ? onSuccess(_value) : onFailure(Errors);
    }

    /// <summary>Runs one of two asynchronous actions: <paramref name="onSuccess"/> with the value, or <paramref name="onFailure"/> with the errors. An <see langword="async"/> lambda picks this overload.</summary>
    /// <param name="onSuccess">Called with the value of a success.</param>
    /// <param name="onFailure">Called with the errors of a failure, at least one.</param>
    /// <returns>The task of the called action.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> or <paramref name="onFailure"/> is <see langword="null"/>; thrown before any action is called.</exception>
    [OverloadResolutionPriority(1)]
    public Task SwitchAsync(Func<T, Task> onSuccess, Func<IReadOnlyList<Error>, Task> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return _isSuccess ? onSuccess(_value) : onFailure(Errors);
    }

    /// <summary>Runs one of two actions that return a <see cref="ValueTask"/>: <paramref name="onSuccess"/> with the value, or <paramref name="onFailure"/> with the errors.</summary>
    /// <param name="onSuccess">Called with the value of a success.</param>
    /// <param name="onFailure">Called with the errors of a failure, at least one.</param>
    /// <returns>The value task of the called action.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> or <paramref name="onFailure"/> is <see langword="null"/>; thrown before any action is called.</exception>
    public ValueTask SwitchAsync(Func<T, ValueTask> onSuccess, Func<IReadOnlyList<Error>, ValueTask> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return _isSuccess ? onSuccess(_value) : onFailure(Errors);
    }

    /// <summary>Transforms the value of a success with an asynchronous function. A failure keeps its errors and <paramref name="selector"/> is not called. An <see langword="async"/> lambda picks this overload.</summary>
    /// <typeparam name="TOut">The type of the new value.</typeparam>
    /// <param name="selector">Computes the new value from the value of a success.</param>
    /// <returns>A task with a success with the new value, or a completed task with a failure with the same errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is <see langword="null"/>; thrown before the task is returned.</exception>
    [OverloadResolutionPriority(1)]
    public Task<Result<TOut>> MapAsync<TOut>(Func<T, Task<TOut>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return _isSuccess ? ToSuccessAsync(selector(_value)) : Task.FromResult(new Result<TOut>(FailureErrors));
    }

    /// <summary>Transforms the value of a success with a function that returns a <see cref="ValueTask{TResult}"/>. A failure keeps its errors and <paramref name="selector"/> is not called.</summary>
    /// <typeparam name="TOut">The type of the new value.</typeparam>
    /// <param name="selector">Computes the new value from the value of a success.</param>
    /// <returns>A value task with a success with the new value, or a completed value task with a failure with the same errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is <see langword="null"/>; thrown before the value task is returned.</exception>
    public ValueTask<Result<TOut>> MapAsync<TOut>(Func<T, ValueTask<TOut>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return _isSuccess ? ToSuccessAsync(selector(_value)) : new ValueTask<Result<TOut>>(new Result<TOut>(FailureErrors));
    }

    /// <summary>Continues a success with an asynchronous operation that can fail. A failure keeps its errors and <paramref name="continuation"/> is not called. An <see langword="async"/> lambda picks this overload.</summary>
    /// <typeparam name="TOut">The type of the value of the operation.</typeparam>
    /// <param name="continuation">The operation, called with the value of a success.</param>
    /// <returns>The task of <paramref name="continuation"/>, or a completed task with a failure with the same errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="continuation"/> is <see langword="null"/>; thrown before the task is returned.</exception>
    [OverloadResolutionPriority(1)]
    public Task<Result<TOut>> ThenAsync<TOut>(Func<T, Task<Result<TOut>>> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        return _isSuccess ? continuation(_value) : Task.FromResult(new Result<TOut>(FailureErrors));
    }

    /// <summary>Continues a success with an operation that can fail and returns a <see cref="ValueTask{TResult}"/>. A failure keeps its errors and <paramref name="continuation"/> is not called.</summary>
    /// <typeparam name="TOut">The type of the value of the operation.</typeparam>
    /// <param name="continuation">The operation, called with the value of a success.</param>
    /// <returns>The value task of <paramref name="continuation"/>, or a completed value task with a failure with the same errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="continuation"/> is <see langword="null"/>; thrown before the value task is returned.</exception>
    public ValueTask<Result<TOut>> ThenAsync<TOut>(Func<T, ValueTask<Result<TOut>>> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        return _isSuccess ? continuation(_value) : new ValueTask<Result<TOut>>(new Result<TOut>(FailureErrors));
    }

    /// <summary>Turns a success into a failure when its value breaks a rule checked asynchronously. A failure keeps its errors and <paramref name="predicate"/> is not called. An <see langword="async"/> lambda picks this overload.</summary>
    /// <param name="predicate">The rule, called with the value of a success; its task has <see langword="true"/> when the value satisfies it.</param>
    /// <param name="error">The declared error of the failure when <paramref name="predicate"/> returns <see langword="false"/>.</param>
    /// <returns>A task with this result, or with a failure with the error of <see cref="ErrorDescriptor.ToError"/> when the value breaks the rule.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> or <paramref name="error"/> is <see langword="null"/>; thrown before the task is returned.</exception>
    [OverloadResolutionPriority(1)]
    public Task<Result<T>> EnsureAsync(Func<T, Task<bool>> predicate, ErrorDescriptor error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);
        return _isSuccess ? KeepWhenAsync(predicate(_value), this, error) : Task.FromResult(this);
    }

    /// <summary>Turns a success into a failure when its value breaks a rule checked by a function that returns a <see cref="ValueTask{TResult}"/>. A failure keeps its errors and <paramref name="predicate"/> is not called.</summary>
    /// <param name="predicate">The rule, called with the value of a success; its value task has <see langword="true"/> when the value satisfies it.</param>
    /// <param name="error">The declared error of the failure when <paramref name="predicate"/> returns <see langword="false"/>.</param>
    /// <returns>A value task with this result, or with a failure with the error of <see cref="ErrorDescriptor.ToError"/> when the value breaks the rule.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> or <paramref name="error"/> is <see langword="null"/>; thrown before the value task is returned.</exception>
    public ValueTask<Result<T>> EnsureAsync(Func<T, ValueTask<bool>> predicate, ErrorDescriptor error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);
        return _isSuccess ? KeepWhenAsync(predicate(_value), this, error) : new ValueTask<Result<T>>(this);
    }

    /// <summary>Runs an asynchronous action with the value of a success, for example to log or record it. A failure keeps its errors and <paramref name="action"/> is not called. An <see langword="async"/> lambda picks this overload.</summary>
    /// <param name="action">The action, called with the value of a success.</param>
    /// <returns>A task with this result, completed after <paramref name="action"/> on a success.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>; thrown before the task is returned.</exception>
    [OverloadResolutionPriority(1)]
    public Task<Result<T>> TapAsync(Func<T, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return _isSuccess ? KeepAfterAsync(action(_value), this) : Task.FromResult(this);
    }

    /// <summary>Runs an action that returns a <see cref="ValueTask"/> with the value of a success. A failure keeps its errors and <paramref name="action"/> is not called.</summary>
    /// <param name="action">The action, called with the value of a success.</param>
    /// <returns>A value task with this result, completed after <paramref name="action"/> on a success.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>; thrown before the value task is returned.</exception>
    public ValueTask<Result<T>> TapAsync(Func<T, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return _isSuccess ? KeepAfterAsync(action(_value), this) : new ValueTask<Result<T>>(this);
    }

    /// <summary>Replaces a failure with a fallback value computed asynchronously from its errors. A success keeps its value and <paramref name="fallback"/> is not called. An <see langword="async"/> lambda picks this overload.</summary>
    /// <param name="fallback">Computes the value from the errors of a failure, at least one.</param>
    /// <returns>A completed task with this success, or a task with a success with the value of <paramref name="fallback"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fallback"/> is <see langword="null"/>; thrown before the task is returned.</exception>
    [OverloadResolutionPriority(1)]
    public Task<Result<T>> ElseAsync(Func<IReadOnlyList<Error>, Task<T>> fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        return _isSuccess ? Task.FromResult(this) : ToSuccessAsync(fallback(FailureErrors));
    }

    /// <summary>Replaces a failure with a fallback value computed from its errors by a function that returns a <see cref="ValueTask{TResult}"/>. A success keeps its value and <paramref name="fallback"/> is not called.</summary>
    /// <param name="fallback">Computes the value from the errors of a failure, at least one.</param>
    /// <returns>A completed value task with this success, or a value task with a success with the value of <paramref name="fallback"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fallback"/> is <see langword="null"/>; thrown before the value task is returned.</exception>
    public ValueTask<Result<T>> ElseAsync(Func<IReadOnlyList<Error>, ValueTask<T>> fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        return _isSuccess ? new ValueTask<Result<T>>(this) : ToSuccessAsync(fallback(FailureErrors));
    }

    /// <inheritdoc/>
    object? IUnionMembers.Value => _isSuccess ? _value : FailureErrors;

    /// <inheritdoc/>
    bool IUnionMembers.HasValue => true;

    /// <inheritdoc/>
    static Result<T> IUnionMembers.Create(T value) => new(value);

    /// <inheritdoc/>
    static Result<T> IUnionMembers.Create(IReadOnlyList<Error> errors) => new(ResultErrors.Copy(errors));

    /// <inheritdoc/>
    bool IUnionMembers.TryGetValue(out T value)
    {
        value = _value;
        return _isSuccess;
    }

    /// <inheritdoc/>
    bool IUnionMembers.TryGetValue(out IReadOnlyList<Error> errors)
    {
        errors = Errors;
        return !_isSuccess;
    }

    private static async Task<Result<TValue>> ToSuccessAsync<TValue>(Task<TValue> pending) => new(await pending.ConfigureAwait(false));

    private static async ValueTask<Result<TValue>> ToSuccessAsync<TValue>(ValueTask<TValue> pending) => new(await pending.ConfigureAwait(false));

    private static async Task<Result<T>> KeepWhenAsync(Task<bool> pending, Result<T> result, ErrorDescriptor error) =>
        await pending.ConfigureAwait(false) ? result : new Result<T>(ResultErrors.Single(error.ToError()));

    private static async ValueTask<Result<T>> KeepWhenAsync(ValueTask<bool> pending, Result<T> result, ErrorDescriptor error) =>
        await pending.ConfigureAwait(false) ? result : new Result<T>(ResultErrors.Single(error.ToError()));

    private static async Task<Result<T>> KeepAfterAsync(Task pending, Result<T> result)
    {
        await pending.ConfigureAwait(false);
        return result;
    }

    private static async ValueTask<Result<T>> KeepAfterAsync(ValueTask pending, Result<T> result)
    {
        await pending.ConfigureAwait(false);
        return result;
    }

    private ReadOnlyCollection<Error> FailureErrors => _errors ?? ResultErrors.Uninitialised;

    /// <inheritdoc/>
    IResult IFailureFactory.CreateFailure(IReadOnlyList<Error> errors) => Failure(errors);
}

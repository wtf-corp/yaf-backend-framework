namespace YAF.Core.Results;

/// <summary>
/// Creates a failed <see cref="Result"/> or <see cref="Result{T}"/> when the result type is known only as a generic parameter, as in
/// pipeline middleware that must stop a request before its handler runs. No reflection.
/// </summary>
/// <example>
/// <code language="csharp">
/// private static TResult NotAllowed&lt;TResult&gt;()
///     where TResult : IResult
///     => ResultFactory.Failure&lt;TResult&gt;([Errors.NotAllowed.ToError()]);
/// </code>
/// </example>
public static class ResultFactory
{
    /// <summary>Creates a failure of the result type <typeparamref name="TResult"/> with the errors in the given order.</summary>
    /// <typeparam name="TResult"><see cref="Result"/> or a <see cref="Result{T}"/>; any other <see cref="IResult"/> is refused.</typeparam>
    /// <param name="errors">The errors, at least one, none <see langword="null"/>. They are copied.</param>
    /// <returns>The failed result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty or contains <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><typeparamref name="TResult"/> is neither <see cref="Result"/> nor a <see cref="Result{T}"/>.</exception>
    public static TResult Failure<TResult>(IReadOnlyList<Error> errors)
        where TResult : IResult
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
        {
            throw new ArgumentException("A failure needs at least one error.", nameof(errors));
        }

        return FactoryOf<TResult>() is { } factory
            ? (TResult)factory.CreateFailure(errors)
            : throw new InvalidOperationException(
                $"ResultFactory creates failures of Result and Result<T> only, not of '{typeof(TResult)}'.");
    }

    // Result and Result<T> are structs, so their default value exists and creates a failure of its own type; for any other type it is null
    // or does not implement IFailureFactory.
    private static IFailureFactory? FactoryOf<TResult>() => default(TResult) as IFailureFactory;
}

/// <summary>Implemented by <see cref="Result"/> and <see cref="Result{T}"/>: creates a failure of the implementing type.</summary>
internal interface IFailureFactory
{
    /// <summary>Creates a failure of the implementing result type.</summary>
    /// <param name="errors">The errors, at least one.</param>
    /// <returns>The failed result, of the implementing type.</returns>
    IResult CreateFailure(IReadOnlyList<Error> errors);
}

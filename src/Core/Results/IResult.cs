namespace YAF.Core.Results;

/// <summary>
/// A <see cref="Result"/> or <see cref="Result{T}"/> seen without its value type, so that pipeline middleware can inspect any result.
/// </summary>
public interface IResult
{
    /// <summary>Gets a value indicating whether the result is a success. A <c>default</c> result is a failure.</summary>
    bool IsSuccess { get; }

    /// <summary>
    /// Gets the errors of a failure, at least one, in order. Empty on success. A <c>default</c> result has the one error of
    /// <see cref="CoreErrors.Uninitialised"/>.
    /// </summary>
    IReadOnlyList<Error> Errors { get; }
}

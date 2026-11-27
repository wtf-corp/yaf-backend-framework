using YAF.Core.Results;

namespace YAF.Application.Validation;

/// <summary>
/// Checks the shape and plausibility of an input, typically a request, before its handler runs. Invariants that depend on aggregate
/// state belong to the domain, not here.
/// </summary>
/// <typeparam name="T">The type of the input.</typeparam>
public interface IValidator<in T>
{
    /// <summary>Checks the input.</summary>
    /// <param name="instance">The input to check.</param>
    /// <param name="ct">The token that cancels the check.</param>
    /// <returns>A success when every rule holds; otherwise a failure with one error per broken rule, each with its target.</returns>
    ValueTask<Result> ValidateAsync(T instance, CancellationToken ct);
}

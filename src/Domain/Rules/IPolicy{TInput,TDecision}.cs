namespace YAF.Domain.Rules;

/// <summary>
/// A decision that varies by tenant, time or configuration. The domain declares the interface and its default implementation; the
/// application layer selects the implementation.
/// </summary>
/// <typeparam name="TInput">The type of the values the decision is made from.</typeparam>
/// <typeparam name="TDecision">The type of the decision.</typeparam>
public interface IPolicy<in TInput, out TDecision>
{
    /// <summary>Makes the decision for the given input.</summary>
    /// <param name="input">The values the decision is made from.</param>
    /// <returns>The decision.</returns>
    TDecision Decide(TInput input);
}

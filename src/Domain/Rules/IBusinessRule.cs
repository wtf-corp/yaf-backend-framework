using YAF.Core.Results;

namespace YAF.Domain.Rules;

/// <summary>
/// A single invariant with its own error: a small named class, constructed with the values it needs, pure and unit-tested on its own.
/// </summary>
/// <remarks>
/// A behaviour checks its rules with <c>Check</c> or <c>CheckAll</c> on the entity base before it mutates anything; a broken rule becomes
/// the error of its <see cref="Error"/> descriptor.
/// </remarks>
public interface IBusinessRule
{
    /// <summary>Gets the descriptor of the error reported when the rule is not satisfied. Declare it in an <see cref="IErrorCatalog"/>.</summary>
    ErrorDescriptor Error { get; }

    /// <summary>Evaluates the rule over the values it was constructed with.</summary>
    /// <returns><see langword="true"/> when the rule holds; otherwise <see langword="false"/>.</returns>
    bool IsSatisfied();
}

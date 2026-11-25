using YAF.Core.Results;
using YAF.Domain.Rules;

namespace Domain.Tests.Fixtures;

internal sealed class QuantityWithinLimit(int quantity, int limit) : IBusinessRule
{
    public ErrorDescriptor Error => OrderErrors.QuantityAboveLimit;

    public bool IsSatisfied() => quantity <= limit;
}

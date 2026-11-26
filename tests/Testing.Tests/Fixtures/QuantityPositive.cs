using YAF.Core.Results;
using YAF.Domain.Rules;

namespace Testing.Tests.Fixtures;

internal sealed class QuantityPositive(int quantity) : IBusinessRule
{
    public ErrorDescriptor Error => BookingErrors.QuantityNotPositive;

    public bool IsSatisfied() => quantity > 0;
}

using YAF.Core.Results;
using YAF.Domain.Rules;

namespace Domain.Tests.Fixtures;

#region business-rule
internal sealed class QuantityPositive(int quantity) : IBusinessRule
{
    public ErrorDescriptor Error => OrderErrors.QuantityNotPositive;

    public bool IsSatisfied() => quantity > 0;
}
#endregion

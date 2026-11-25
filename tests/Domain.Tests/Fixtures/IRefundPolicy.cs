using YAF.Domain.Rules;

namespace Domain.Tests.Fixtures;

#region policy
// Decides the refund, in percent, for a cancellation the given number of days before the start.
internal interface IRefundPolicy : IPolicy<int, decimal>;

internal sealed class DefaultRefundPolicy : IRefundPolicy
{
    public decimal Decide(int input) => input >= 14 ? 100m : 50m;
}
#endregion

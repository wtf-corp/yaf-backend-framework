using Domain.Tests.Fixtures;
using YAF.Domain.Rules;

namespace Domain.Tests.Rules;

public sealed class PolicyTests
{
    [Fact]
    public void Decide_DefaultPolicy_ReturnsDecision()
    {
        var policy = new DefaultRefundPolicy();

        var refund = policy.Decide(3);

        refund.Should().Be(50m);
    }

    [Fact]
    public void Policy_IsContravariantInInputAndCovariantInDecision()
    {
        IPolicy<object, string> general = new DescribingPolicy();

        IPolicy<string, object> specific = general;

        specific.Decide("input").Should().Be("String");
    }

    private sealed class DescribingPolicy : IPolicy<object, string>
    {
        public string Decide(object input) => input.GetType().Name;
    }
}

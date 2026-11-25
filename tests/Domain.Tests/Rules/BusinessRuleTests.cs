using Domain.Tests.Fixtures;

namespace Domain.Tests.Rules;

public sealed class BusinessRuleTests
{
    [Fact]
    public void IsSatisfied_BrokenRule_ExposesItsDescriptor()
    {
        var rule = new QuantityPositive(0);

        var satisfied = rule.IsSatisfied();

        satisfied.Should().BeFalse();
        rule.Error.Code.Should().Be(OrderErrors.QuantityNotPositive.Code);
    }

    [Fact]
    public void IsSatisfied_KeptRule_ReturnsTrue()
    {
        var rule = new QuantityPositive(1);

        var satisfied = rule.IsSatisfied();

        satisfied.Should().BeTrue();
    }
}

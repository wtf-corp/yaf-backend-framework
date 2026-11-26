using YAF.Domain.ValueObjects;

namespace Domain.Tests.ValueObjects;

public sealed class PercentageTests
{
    [Fact]
    public void Create_Above100_FailsWithOutOfRange()
    {
        var result = Percentage.Create(100.01m);

        result.Errors.Select(e => e.Code).Should().Equal(ValueObjectErrors.PercentageOutOfRange.Code);
        ValueObjectErrors.PercentageOutOfRange.Code.Should().Be("core.percentage.out_of_range");
    }

    [Fact]
    public void Create_Negative_Fails()
    {
        var result = Percentage.Create(-0.01m);

        result.Errors.Select(e => e.Code).Should().Equal(ValueObjectErrors.PercentageOutOfRange.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Create_Bounds_Succeed(int value)
    {
        var result = Percentage.Create(value);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(value);
    }

    [Fact]
    public void Of_Money_KeepsTheCurrency()
    {
        var eur = Currency.FromCode("EUR").Value;
        var percentage = Percentage.Create(12.5m).Value;

        var share = percentage.Of(new Money(10.01m, eur));

        share.Should().Be(new Money(1.25125m, eur));
        share.Currency.Should().Be(eur);
    }

    [Fact]
    public void Of_Decimal_Multiplies()
    {
        var percentage = Percentage.Create(33m).Value;

        var share = percentage.Of(10m);

        share.Should().Be(3.3m);
    }

    [Fact]
    public void Of_AmountNearDecimalMax_ReturnsTheShare()
    {
        var percentage = Percentage.Create(50m).Value;

        var share = percentage.Of(decimal.MaxValue);

        share.Should().Be(decimal.MaxValue * 0.5m);
    }
}

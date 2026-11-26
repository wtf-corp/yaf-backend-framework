using YAF.Domain.ValueObjects;

namespace Domain.Tests.ValueObjects;

public sealed class CurrencyTests
{
    [Fact]
    public void FromCode_Unknown_FailsWithUnknownCurrency()
    {
        var result = Currency.FromCode("ABC");

        result.Errors.Select(e => e.Code).Should().Equal(ValueObjectErrors.UnknownCurrency.Code);
        ValueObjectErrors.UnknownCurrency.Code.Should().Be("core.currency.unknown");
        result.Errors[0].Arguments.Should().Contain("Code", "ABC");
    }

    [Fact]
    public void FromCode_Eur_HasTwoMinorUnits()
    {
        var result = Currency.FromCode("EUR");

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("EUR");
        result.Value.MinorUnits.Should().Be(2);
    }

    [Fact]
    public void FromCode_Jpy_HasNone()
    {
        var result = Currency.FromCode("JPY");

        result.Value.MinorUnits.Should().Be(0);
    }

    [Fact]
    public void FromCode_Bhd_HasThree()
    {
        var result = Currency.FromCode("BHD");

        result.Value.MinorUnits.Should().Be(3);
    }

    [Fact]
    public void FromCode_Clf_HasFour()
    {
        var result = Currency.FromCode("CLF");

        result.Value.MinorUnits.Should().Be(4);
    }

    [Fact]
    public void FromCode_LowerCase_Fails()
    {
        var result = Currency.FromCode("eur");

        result.Errors.Select(e => e.Code).Should().Equal(ValueObjectErrors.UnknownCurrency.Code);
    }

    [Fact]
    public void FromCode_CodeWithoutMinorUnits_Fails()
    {
        var result = Currency.FromCode("XAU");

        result.Errors.Select(e => e.Code).Should().Equal(ValueObjectErrors.UnknownCurrency.Code);
    }

    [Fact]
    public void FromCode_Null_Throws()
    {
        var act = () => Currency.FromCode(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void All_HoldsEveryCodeOnce()
    {
        var codes = Currency.All.Select(c => c.Code).ToList();

        codes.Should().HaveCount(165).And.OnlyHaveUniqueItems().And.Contain(["EUR", "USD", "JPY", "BHD", "UYW"]);
        codes.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public void All_EveryCurrency_IsFoundByItsCode()
    {
        var found = Currency.All.Select(c => Currency.FromCode(c.Code).Value);

        found.Should().Equal(Currency.All);
    }

    [Fact]
    public void Equals_SameCode_IsEqual()
    {
        var first = Currency.FromCode("USD").Value;
        var second = Currency.All.Single(c => c.Code == "USD");

        first.Should().Be(second);
        (first == second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
        first.Should().NotBe(Currency.FromCode("EUR").Value);
    }

    [Fact]
    public void Currency_IsAValueObject()
    {
        var currency = Currency.FromCode("EUR").Value;

        currency.Should().BeAssignableTo<YAF.Domain.ValueObject>();
    }
}

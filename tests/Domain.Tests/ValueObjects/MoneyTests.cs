using YAF.Domain.ValueObjects;

namespace Domain.Tests.ValueObjects;

public sealed class MoneyTests
{
    private static readonly Currency s_eur = Currency.FromCode("EUR").Value;
    private static readonly Currency s_usd = Currency.FromCode("USD").Value;
    private static readonly Currency s_jpy = Currency.FromCode("JPY").Value;

    #region money-allocate
    [Fact]
    public void Allocate_HundredInThree_GivesTheRemainderToTheFirst()
    {
        var total = new Money(100.00m, s_eur);

        var shares = total.Allocate(3);

        shares.Select(s => s.Amount).Should().Equal(33.34m, 33.33m, 33.33m);
        shares.Should().OnlyContain(s => s.Currency == s_eur);
    }
    #endregion

    [Fact]
    public void Allocate_ByRatios_SumsToTheAmount()
    {
        var total = new Money(0.05m, s_eur);

        var shares = total.Allocate(70, 30);

        shares.Select(s => s.Amount).Should().Equal(0.04m, 0.01m);
        shares.Sum(s => s.Amount).Should().Be(0.05m);
    }

    [Fact]
    public void Allocate_NoMinorUnits_SplitsWholeUnits()
    {
        var total = new Money(100m, s_jpy);

        var shares = total.Allocate(3);

        shares.Select(s => s.Amount).Should().Equal(34m, 33m, 33m);
    }

    [Fact]
    public void Allocate_NegativeAmount_SumsToTheAmount()
    {
        var total = new Money(-100.00m, s_eur);

        var shares = total.Allocate(3);

        shares.Select(s => s.Amount).Should().Equal(-33.34m, -33.33m, -33.33m);
        shares.Sum(s => s.Amount).Should().Be(-100.00m);
    }

    [Fact]
    public void Allocate_RestFinerThanAMinorUnit_GoesToTheFirst()
    {
        var total = new Money(10.005m, s_eur);

        var shares = total.Allocate(1, 1);

        shares.Select(s => s.Amount).Should().Equal(5.005m, 5.00m);
    }

    [Fact]
    public void Allocate_ZeroRatio_GetsNothing()
    {
        var total = new Money(1.00m, s_eur);

        var shares = total.Allocate(0, 1, 1, 1);

        shares.Select(s => s.Amount).Should().Equal(0m, 0.34m, 0.33m, 0.33m);
    }

    [Fact]
    public void Allocate_NoRatio_Throws()
    {
        var total = new Money(1m, s_eur);

        total.Invoking(m => m.Allocate([])).Should().Throw<ArgumentException>();
        total.Invoking(m => m.Allocate(0)).Should().Throw<ArgumentException>();
        total.Invoking(m => m.Allocate(1, -1)).Should().Throw<ArgumentException>();
        total.Invoking(m => m.Allocate(0, 0)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Add_SameCurrency_Adds()
    {
        var result = new Money(1.50m, s_eur).Add(new Money(2.25m, s_eur));

        result.Value.Should().Be(new Money(3.75m, s_eur));
    }

    [Fact]
    public void Add_OtherCurrency_FailsWithCurrencyMismatch()
    {
        var result = new Money(1m, s_eur).Add(new Money(1m, s_usd));

        result.Errors.Select(e => e.Code).Should().Equal(ValueObjectErrors.CurrencyMismatch.Code);
        ValueObjectErrors.CurrencyMismatch.Code.Should().Be("core.money.currency_mismatch");
        result.Errors[0].Arguments.Should().Contain("Left", "EUR").And.Contain("Right", "USD");
    }

    [Fact]
    public void Subtract_SameCurrency_Subtracts()
    {
        var result = new Money(1.50m, s_eur).Subtract(new Money(2.25m, s_eur));

        result.Value.Should().Be(new Money(-0.75m, s_eur));
    }

    [Fact]
    public void Subtract_OtherCurrency_FailsWithCurrencyMismatch()
    {
        var result = new Money(1m, s_eur).Subtract(new Money(1m, s_jpy));

        result.Errors.Select(e => e.Code).Should().Equal(ValueObjectErrors.CurrencyMismatch.Code);
        result.Errors[0].Arguments.Should().Contain("Left", "EUR").And.Contain("Right", "JPY");
    }

    [Fact]
    public void Multiply_DoesNotRound()
    {
        var product = new Money(10.00m, s_eur).Multiply(0.333m);

        product.Amount.Should().Be(3.33m);
        new Money(1.00m, s_eur).Multiply(0.125m).Amount.Should().Be(0.125m);
    }

    [Fact]
    public void Round_ToMinorUnits_UsesTheMode()
    {
        var amount = new Money(2.125m, s_eur);

        amount.Round(MidpointRounding.ToEven).Amount.Should().Be(2.12m);
        amount.Round(MidpointRounding.AwayFromZero).Amount.Should().Be(2.13m);
        new Money(2.5m, s_jpy).Round(MidpointRounding.ToEven).Amount.Should().Be(2m);
    }

    [Fact]
    public void Constructor_NullCurrency_Throws()
    {
        var act = () => new Money(1m, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Equals_SameAmountAndCurrency_IsEqual()
    {
        new Money(1.5m, s_eur).Should().Be(new Money(1.50m, s_eur));
        new Money(1.5m, s_eur).Should().NotBe(new Money(1.5m, s_usd));
        new Money(1.5m, s_eur).Should().BeAssignableTo<YAF.Domain.ValueObject>();
    }
}

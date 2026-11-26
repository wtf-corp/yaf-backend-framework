using System.Reflection;
using YAF.Core.Classification;
using YAF.Core.Results;
using YAF.Domain.ValueObjects;

namespace Domain.Tests.ValueObjects;

public sealed class EmailAddressTests
{
    [Fact]
    public void Create_TwoAtSigns_FailsWithInvalidEmailAddress()
    {
        var result = EmailAddress.Create("ada@lovelace@example.org");

        result.Errors.Select(e => e.Code).Should().Equal(ValueObjectErrors.InvalidEmailAddress.Code);
        result.FirstError.Kind.Should().Be(ErrorKind.Validation);
        ValueObjectErrors.InvalidEmailAddress.Code.Should().Be("core.email_address.invalid");
    }

    [Theory]
    [InlineData("@example.org")]
    [InlineData("ada@")]
    [InlineData("ada.example.org")]
    [InlineData("")]
    public void Create_EmptyLocalPart_Fails(string value)
    {
        var result = EmailAddress.Create(value);

        result.Errors.Select(e => e.Code).Should().Equal(ValueObjectErrors.InvalidEmailAddress.Code);
        result.FirstError.Kind.Should().Be(ErrorKind.Validation);
    }

    [Fact]
    public void Create_LocalPartOf64Octets_Succeeds()
    {
        var result = EmailAddress.Create(new string('a', 64) + "@example.org");

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(65, 'a')]
    [InlineData(33, 'ä')]
    public void Create_LocalPartOf65Octets_Fails(int length, char letter)
    {
        var result = EmailAddress.Create(new string(letter, length) + "@example.org");

        result.Errors.Select(e => e.Code).Should().Equal(ValueObjectErrors.InvalidEmailAddress.Code);
        result.FirstError.Kind.Should().Be(ErrorKind.Validation);
    }

    [Fact]
    public void Create_254Octets_Succeeds()
    {
        var result = EmailAddress.Create(new string('a', 64) + "@" + new string('b', 185) + ".org");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_255Octets_Fails()
    {
        var result = EmailAddress.Create(new string('a', 64) + "@" + new string('b', 186) + ".org");

        result.Errors.Select(e => e.Code).Should().Equal(ValueObjectErrors.InvalidEmailAddress.Code);
        result.FirstError.Kind.Should().Be(ErrorKind.Validation);
    }

    [Theory]
    [InlineData("ada lovelace@example.org")]
    [InlineData("ada@example.org ")]
    [InlineData("ada@exa\tmple.org")]
    [InlineData("ada\u0000@example.org")]
    public void Create_WhiteSpaceOrControlCharacter_Fails(string value)
    {
        var result = EmailAddress.Create(value);

        result.Errors.Select(e => e.Code).Should().Equal(ValueObjectErrors.InvalidEmailAddress.Code);
    }

    [Fact]
    public void Create_UpperCaseDomain_LowersIt()
    {
        var result = EmailAddress.Create("Ada.Lovelace@Example.ORG");

        result.Value.Value.Should().Be("Ada.Lovelace@example.org");
        result.Value.Should().Be(EmailAddress.Create("Ada.Lovelace@example.org").Value);
    }

    [Fact]
    public void Type_IsClassifiedPersonalContact()
    {
        var attribute = typeof(EmailAddress).GetCustomAttribute<PersonalDataAttribute>();

        attribute.Should().NotBeNull();
        attribute!.Category.Should().Be(PersonalDataCategory.Contact);
    }

    [Fact]
    public void ToString_DoesNotContainTheAddress()
    {
        var address = EmailAddress.Create("ada@example.org").Value;

        address.ToString().Should().Be("***");
        $"{address}".Should().NotContain("ada");
    }
}

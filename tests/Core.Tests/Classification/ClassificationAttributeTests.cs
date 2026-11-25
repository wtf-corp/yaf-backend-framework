using System.Reflection;
using Microsoft.Extensions.Compliance.Classification;
using YAF.Core.Classification;

namespace Core.Tests.Classification;

public sealed class ClassificationAttributeTests
{
    [Fact]
    public void PersonalData_Classification_IsYafPersonal()
    {
        var attribute = new PersonalDataAttribute(PersonalDataCategory.Contact);

        attribute.Classification.Should().Be(YafTaxonomy.Personal);
        attribute.Classification.TaxonomyName.Should().Be("YAF");
    }

    [Fact]
    public void PersonalData_Defaults_ProvidedBySubjectWithoutPropagation()
    {
        var attribute = new PersonalDataAttribute(PersonalDataCategory.Identity);

        attribute.Category.Should().Be(PersonalDataCategory.Identity);
        attribute.Purpose.Should().BeNull();
        attribute.ProvidedBySubject.Should().BeTrue();
        attribute.PropagateRectification.Should().BeFalse();
    }

    [Fact]
    public void PersonalData_NamedArguments_AreKept()
    {
        var attribute = typeof(ISample).GetProperty(nameof(ISample.Email))!.GetCustomAttribute<PersonalDataAttribute>()!;

        attribute.Purpose.Should().Be("registration");
        attribute.ProvidedBySubject.Should().BeFalse();
        attribute.PropagateRectification.Should().BeTrue();
    }

    [Fact]
    public void SensitivePersonalData_Classification_IsYafSensitivePersonal()
    {
        var attribute = new SensitivePersonalDataAttribute(PersonalDataCategory.Health);

        attribute.Classification.Should().Be(YafTaxonomy.SensitivePersonal);
        attribute.Category.Should().Be(PersonalDataCategory.Health);
    }

    [Fact]
    public void OtherAttributes_Classification_IsTheirYafValue()
    {
        new PublicDataAttribute().Classification.Should().Be(YafTaxonomy.Public);
        new InternalDataAttribute().Classification.Should().Be(YafTaxonomy.Internal);
        new SecretDataAttribute().Classification.Should().Be(YafTaxonomy.Secret);
    }

    [Fact]
    public void YafTaxonomy_Values_AreDistinctAndInTaxonomyYaf()
    {
        DataClassification[] values =
            [YafTaxonomy.Public, YafTaxonomy.Internal, YafTaxonomy.Personal, YafTaxonomy.SensitivePersonal, YafTaxonomy.Secret];

        values.Should().OnlyHaveUniqueItems();
        values.Should().AllSatisfy(value => value.TaxonomyName.Should().Be(YafTaxonomy.Name));
        YafTaxonomy.Name.Should().Be("YAF");
    }

    [Fact]
    public void PersonalDataCategory_Names_AreTheNineOfTheSpec()
    {
        Enum.GetNames<PersonalDataCategory>().Should().Equal(
            "Identity", "Contact", "Location", "Financial", "Health", "Biometric", "Behavioural", "Credentials", "Other");
    }

    [Fact]
    public void PersonalData_OnMember_IsFoundAsDataClassificationAttribute()
    {
        var attribute = typeof(ISample).GetProperty(nameof(ISample.Email))!.GetCustomAttribute<DataClassificationAttribute>();

        attribute.Should().BeOfType<PersonalDataAttribute>();
    }

    [Fact]
    public void PersonalData_OnType_IsReadFromTheType()
    {
        var attribute = typeof(SampleEmailAddress).GetCustomAttribute<DataClassificationAttribute>();

        attribute!.Classification.Should().Be(YafTaxonomy.Personal);
    }

    private interface ISample
    {
        [PersonalData(PersonalDataCategory.Contact, Purpose = "registration", ProvidedBySubject = false, PropagateRectification = true)]
        string Email { get; set; }
    }

    [PersonalData(PersonalDataCategory.Contact)]
    private readonly record struct SampleEmailAddress(string Value);
}

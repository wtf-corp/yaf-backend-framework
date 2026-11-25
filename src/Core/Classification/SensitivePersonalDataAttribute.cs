using Microsoft.Extensions.Compliance.Classification;

namespace YAF.Core.Classification;

/// <summary>
/// Classifies a member or a type as <see cref="YafTaxonomy.SensitivePersonal"/>: special categories of personal data, financial
/// account data, precise location and a person's credentials. Always encrypted at rest with the data subject's key, erased from
/// logs, refused in integration events.
/// </summary>
/// <remarks>
/// <see cref="StoredInClearAttribute"/> is not allowed on such a member.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Field | AttributeTargets.Property
        | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter,
    AllowMultiple = false)]
public sealed class SensitivePersonalDataAttribute : DataClassificationAttribute
{
    /// <summary>Initializes a new instance of the <see cref="SensitivePersonalDataAttribute"/> class.</summary>
    /// <param name="category">What kind of personal data the member holds.</param>
    public SensitivePersonalDataAttribute(PersonalDataCategory category)
        : base(YafTaxonomy.SensitivePersonal)
    {
        Category = category;
    }

    /// <summary>Gets what kind of personal data the member holds.</summary>
    public PersonalDataCategory Category { get; }
}

using Microsoft.Extensions.Compliance.Classification;

namespace YAF.Core.Classification;

/// <summary>
/// Classifies a member or a type as <see cref="YafTaxonomy.Personal"/>: data that identifies or describes a person, such as names,
/// contact data, identifiers, addresses and online ids. Encrypted at rest with the data subject's key unless the member is
/// <see cref="StoredInClearAttribute">stored in clear</see>; logged as a keyed hash.
/// </summary>
/// <remarks>
/// On a positional record, target the property: <c>[property: PersonalData(…)]</c>. Classification on a type applies to every member of that type.
/// </remarks>
/// <example>
/// <code language="csharp">
/// [PersonalData(PersonalDataCategory.Contact, Purpose = "registration")]
/// string Email { get; set; }
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Field | AttributeTargets.Property
        | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter,
    AllowMultiple = false)]
public sealed class PersonalDataAttribute : DataClassificationAttribute
{
    /// <summary>Initializes a new instance of the <see cref="PersonalDataAttribute"/> class.</summary>
    /// <param name="category">What kind of personal data the member holds.</param>
    public PersonalDataAttribute(PersonalDataCategory category)
        : base(YafTaxonomy.Personal)
    {
        Category = category;
    }

    /// <summary>Gets what kind of personal data the member holds.</summary>
    public PersonalDataCategory Category { get; }

    /// <summary>
    /// Gets or sets the purpose the data is processed for: an entry of the service's purpose catalogue (<c>IPurposeCatalog</c>).
    /// A startup check and a test verify it, not the compiler.
    /// </summary>
    public string? Purpose { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the person supplied the data themselves. It decides whether the member is part of a
    /// portability export. The default is <see langword="true"/>.
    /// </summary>
    public bool ProvidedBySubject { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a rectification of this member must be passed on to the other contexts that hold a copy.
    /// The default is <see langword="false"/>.
    /// </summary>
    public bool PropagateRectification { get; set; }
}

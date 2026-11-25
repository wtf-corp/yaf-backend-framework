using Microsoft.Extensions.Compliance.Classification;

namespace YAF.Core.Classification;

/// <summary>
/// The one data taxonomy of YAF, <c>YAF</c>, and its five classifications. The classification attributes carry these values;
/// redaction, encryption at rest, the data inventory and the analyzers act on them.
/// </summary>
/// <remarks>
/// Built on <see cref="DataClassification"/> from <c>Microsoft.Extensions.Compliance.Abstractions</c>, so a redactor registered
/// for one of these values applies to every member classified with the matching attribute.
/// </remarks>
public static class YafTaxonomy
{
    /// <summary>The name of the taxonomy: <c>YAF</c>.</summary>
    public const string TaxonomyName = "YAF";

    /// <summary>Ids, codes, statuses and published content. Carried by <see cref="PublicDataAttribute"/>.</summary>
    public static readonly DataClassification Public = new(TaxonomyName, nameof(Public));

    /// <summary>Business data that is not about a person. Carried by <see cref="InternalDataAttribute"/>.</summary>
    public static readonly DataClassification Internal = new(TaxonomyName, nameof(Internal));

    /// <summary>Data that identifies or describes a person. Carried by <see cref="PersonalDataAttribute"/>.</summary>
    public static readonly DataClassification Personal = new(TaxonomyName, nameof(Personal));

    /// <summary>
    /// Special categories of personal data, financial account data, precise location and a person's credentials.
    /// Carried by <see cref="SensitivePersonalDataAttribute"/>.
    /// </summary>
    public static readonly DataClassification SensitivePersonal = new(TaxonomyName, nameof(SensitivePersonal));

    /// <summary>Data that grants access: passwords, tokens, keys, connection strings, presigned URLs. Carried by <see cref="SecretDataAttribute"/>.</summary>
    public static readonly DataClassification Secret = new(TaxonomyName, nameof(Secret));
}

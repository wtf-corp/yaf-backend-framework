using Microsoft.Extensions.Compliance.Classification;

namespace YAF.Core.Classification;

/// <summary>
/// Classifies a member or a type as <see cref="YafTaxonomy.Public"/>: ids, codes, statuses and published content.
/// Stored in clear, logged as is, allowed in integration events.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Field | AttributeTargets.Property
        | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter,
    AllowMultiple = false)]
public sealed class PublicDataAttribute : DataClassificationAttribute
{
    /// <summary>Initializes a new instance of the <see cref="PublicDataAttribute"/> class.</summary>
    public PublicDataAttribute()
        : base(YafTaxonomy.Public)
    {
    }
}

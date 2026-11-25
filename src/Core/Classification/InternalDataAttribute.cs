using Microsoft.Extensions.Compliance.Classification;

namespace YAF.Core.Classification;

/// <summary>
/// Classifies a member or a type as <see cref="YafTaxonomy.Internal"/>: business data that is not about a person.
/// Stored in clear, logged as is, allowed in integration events.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Field | AttributeTargets.Property
        | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter,
    AllowMultiple = false)]
public sealed class InternalDataAttribute : DataClassificationAttribute
{
    /// <summary>Initializes a new instance of the <see cref="InternalDataAttribute"/> class.</summary>
    public InternalDataAttribute()
        : base(YafTaxonomy.Internal)
    {
    }
}

using Microsoft.Extensions.Compliance.Classification;

namespace YAF.Core.Classification;

/// <summary>
/// Classifies a member or a type as <see cref="YafTaxonomy.Secret"/>: data that grants access, such as passwords, tokens, keys,
/// connection strings and presigned URLs. Encrypted at rest with the key ring, logged as <c>***</c>, refused in integration events and jobs.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Field | AttributeTargets.Property
        | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter,
    AllowMultiple = false)]
public sealed class SecretDataAttribute : DataClassificationAttribute
{
    /// <summary>Initializes a new instance of the <see cref="SecretDataAttribute"/> class.</summary>
    public SecretDataAttribute()
        : base(YafTaxonomy.Secret)
    {
    }
}

namespace YAF.Core.Classification;

/// <summary>
/// Keeps a <see cref="PersonalDataAttribute">personal data</see> member unencrypted, so the store can sort, range-filter or
/// prefix-search it. The member and its reason are listed in the data inventory; it is erased by overwriting, not by destroying a key.
/// </summary>
/// <remarks>
/// Not allowed on <see cref="SensitivePersonalDataAttribute">sensitive personal data</see> or <see cref="SecretDataAttribute">secret data</see>.
/// </remarks>
/// <example>
/// <code language="csharp">
/// [PersonalData(PersonalDataCategory.Identity, Purpose = "registration")]
/// [StoredInClear("attendee list is sorted by surname")]
/// string Surname { get; set; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class StoredInClearAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="StoredInClearAttribute"/> class.</summary>
    /// <param name="reason">Why the store needs the value in clear.</param>
    public StoredInClearAttribute(string reason)
    {
        Reason = reason;
    }

    /// <summary>Gets why the store needs the value in clear.</summary>
    public string Reason { get; }
}

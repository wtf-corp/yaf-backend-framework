namespace YAF.Core.Classification;

/// <summary>
/// Pins the logical field id that a protected member's ciphertext is bound to. The id defaults to <c>&lt;memento type&gt;.&lt;member&gt;</c>
/// when the member is first declared; after a rename of the type or the member, the recorded id is pinned with this attribute so
/// existing ciphertext stays readable.
/// </summary>
/// <example>
/// <code language="csharp">
/// [FieldId("registration.attendee-email")]
/// string Email { get; set; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class FieldIdAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="FieldIdAttribute"/> class.</summary>
    /// <param name="id">The logical field id.</param>
    public FieldIdAttribute(string id)
    {
        Id = id;
    }

    /// <summary>Gets the logical field id.</summary>
    public string Id { get; }
}

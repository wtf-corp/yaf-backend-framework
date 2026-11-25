namespace YAF.Domain.Mementos;

/// <summary>The contract of a memento whose state belongs to one tenant.</summary>
/// <remarks>Child entities carry no tenant of their own; the tenant of their aggregate root covers them.</remarks>
public interface ITenantOwnedMemento
{
    /// <summary>
    /// Gets or sets the value of the owning tenant's id. Filled by persistence from the tenant context; a memento whose tenant does not match
    /// the context is refused. The domain does not set it.
    /// </summary>
    Guid TenantId { get; set; }
}

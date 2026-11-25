namespace YAF.Domain.Mementos;

/// <summary>The contract of a memento that carries a concurrency token: the version of an aggregate root.</summary>
/// <remarks>Child entities have no version of their own; the version of their aggregate root covers them.</remarks>
public interface IVersionedMemento
{
    /// <summary>
    /// Gets or sets the concurrency token. Filled by persistence, with a new token for the root each time the aggregate is saved in a
    /// transaction.
    /// </summary>
    Guid Version { get; set; }
}

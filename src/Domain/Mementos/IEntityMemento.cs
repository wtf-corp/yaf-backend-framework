namespace YAF.Domain.Mementos;

/// <summary>
/// The base contract of every memento: the stored state of an entity. A memento interface is declared by the domain, nested in its
/// entity, and implemented by a data class in the service's infrastructure.
/// </summary>
/// <remarks>
/// A memento carries no behaviour: no encryption, no input checks, no logging. Derive an aggregate root's memento from
/// <see cref="IAggregateMemento"/> and a child entity's from <see cref="IOwnedEntityMemento"/>.
/// </remarks>
public interface IEntityMemento
{
    /// <summary>
    /// Gets or sets the value of the entity's typed id. Filled by the domain: the entity writes it when it takes a snapshot and checks it
    /// when it restores.
    /// </summary>
    Guid Id { get; set; }
}

namespace YAF.Domain;

/// <summary>
/// An entity that can be restored from its memento: the persistence layer calls <see cref="Restore"/> to rebuild the entity from
/// stored state.
/// </summary>
/// <typeparam name="TSelf">The entity type itself.</typeparam>
/// <typeparam name="TMemento">The entity's memento interface, nested in the entity.</typeparam>
public interface IOriginator<TSelf, TMemento>
{
    /// <summary>
    /// Rebuilds an entity from <paramref name="memento"/>. Implement it by constructing the entity with the id the memento holds and
    /// calling <c>RestoreFrom</c>.
    /// </summary>
    /// <param name="memento">The stored state. It is trusted and not validated.</param>
    /// <returns>The restored entity.</returns>
    /// <exception cref="Mementos.CorruptStateException">The memento cannot be restored, which signals a bug or tampering.</exception>
    static abstract TSelf Restore(TMemento memento);
}

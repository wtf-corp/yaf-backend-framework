namespace YAF.Domain.Mementos;

/// <summary>
/// The base contract of a child entity's memento: its id and the stamps of its creation and last modification. It has no tenant and no
/// version; those of the aggregate root cover it.
/// </summary>
/// <remarks>The domain fills <see cref="IEntityMemento.Id"/>; persistence fills the stamps.</remarks>
public interface IOwnedEntityMemento : IEntityMemento, IStampedMemento;

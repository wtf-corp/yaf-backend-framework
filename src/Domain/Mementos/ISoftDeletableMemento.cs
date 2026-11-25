namespace YAF.Domain.Mementos;

/// <summary>The contract of a memento whose state is marked as deleted instead of being removed from the store.</summary>
/// <remarks>Both members are filled by persistence when the entity is removed; the domain does not set them.</remarks>
public interface ISoftDeletableMemento
{
    /// <summary>Gets or sets the time at which the state was removed, or <see langword="null"/> while it is not. Filled by persistence.</summary>
    DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the value of the id of the user who removed the state, or <see langword="null"/> while it is not removed. Filled by
    /// persistence.
    /// </summary>
    Guid? DeletedBy { get; set; }
}

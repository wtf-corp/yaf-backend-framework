namespace YAF.Domain.Mementos;

/// <summary>The contract of a memento that records when and by whom its state was created and last modified.</summary>
/// <remarks>
/// Every stamp is filled by persistence, from its <see cref="TimeProvider"/> and the identity context; the domain does not set them. A save
/// always runs with an execution context, so a stamp always has a user.
/// </remarks>
public interface IStampedMemento
{
    /// <summary>Gets or sets the time at which the state was first saved. Filled by persistence.</summary>
    DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the value of the id of the user who first saved the state. Filled by persistence.</summary>
    Guid CreatedBy { get; set; }

    /// <summary>Gets or sets the time at which the state was last saved. Filled by persistence.</summary>
    DateTimeOffset ModifiedAt { get; set; }

    /// <summary>Gets or sets the value of the id of the user who last saved the state. Filled by persistence.</summary>
    Guid ModifiedBy { get; set; }
}

namespace YAF.Domain.Mementos;

/// <summary>The contract of a memento whose personal data belongs to one data subject.</summary>
/// <remarks>The data subject's id lets the framework find the state when the subject asks for access or erasure.</remarks>
public interface IDataSubjectOwnedMemento
{
    /// <summary>
    /// Gets or sets the value of the id of the data subject the state belongs to. Filled by the domain when the entity takes a snapshot.
    /// </summary>
    Guid DataSubjectId { get; set; }
}

namespace YAF.Domain.Events;

/// <summary>
/// The base record of a domain event: an immutable, past-tense fact that carries ids and the values a handler needs.
/// </summary>
/// <remarks>
/// An event never carries the aggregate instance, and carries personal data only when a handler in the same bounded context needs it.
/// Raise it inside the behaviour that causes it.
/// </remarks>
/// <example>
/// <code language="csharp">
/// public sealed record SessionPublished(SessionId SessionId, int Capacity) : DomainEvent;
/// </code>
/// </example>
public abstract record DomainEvent : IDomainEvent
{
    /// <summary>Gets the unique id of this occurrence of the event: a new version 7 UUID unless it is set when the event is restored.</summary>
    public Guid EventId { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// Gets the instant the event occurred. It is <see langword="default"/> until the aggregate tracker stamps it from the
    /// <see cref="TimeProvider"/>; domain code does not set it.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }
}

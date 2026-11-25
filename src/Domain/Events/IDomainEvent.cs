namespace YAF.Domain.Events;

/// <summary>
/// A fact that happened inside an aggregate and that handlers in the same bounded context react to. Derive events from
/// <see cref="DomainEvent"/> rather than implementing this interface by hand.
/// </summary>
/// <remarks>
/// An aggregate queues its events with <c>Raise</c>; they are dispatched in process, inside the command's transaction, after the changes
/// that raised them have been flushed and before commit.
/// </remarks>
public interface IDomainEvent
{
    /// <summary>Gets the unique id of this occurrence of the event.</summary>
    Guid EventId { get; }

    /// <summary>
    /// Gets the instant the event occurred. It is <see langword="default"/> until the aggregate tracker stamps it from the
    /// <see cref="TimeProvider"/>.
    /// </summary>
    DateTimeOffset OccurredAt { get; }
}

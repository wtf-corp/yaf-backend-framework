using YAF.Domain;
using YAF.Domain.Events;

namespace YAF.Testing;

/// <summary>
/// Asserts that a behaviour of an aggregate raised a domain event. A failed assertion throws <see cref="YafAssertionException"/>, whose
/// message names the event types that were raised and never a value.
/// </summary>
/// <example>
/// <code>
/// booking.AddLine(lineId, 2);
///
/// var added = DomainEventAssert.Raised&lt;BookingLineAdded&gt;(booking);
/// </code>
/// </example>
public static class DomainEventAssert
{
    /// <summary>
    /// Returns the first event of type <typeparamref name="TEvent"/> in the queue of <paramref name="aggregate"/>, without dequeuing it.
    /// </summary>
    /// <typeparam name="TEvent">The type of the expected event.</typeparam>
    /// <param name="aggregate">The aggregate whose behaviour was called; its queue is left as it was.</param>
    /// <returns>The first queued event of type <typeparamref name="TEvent"/>, for further assertions.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="aggregate"/> is <see langword="null"/>.</exception>
    /// <exception cref="YafAssertionException">No queued event has that type; the message lists the types that were raised.</exception>
    public static TEvent Raised<TEvent>(IAggregateRoot aggregate)
        where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        var events = aggregate.DomainEvents;
        foreach (var domainEvent in events)
        {
            if (domainEvent is TEvent match)
            {
                return match;
            }
        }

        var raisedTypes = events.Select(domainEvent => $"'{domainEvent.GetType().Name}'").Distinct(StringComparer.Ordinal);
        var raised = events.Count == 0 ? "no domain event" : "the domain events " + string.Join(", ", raisedTypes);
        throw new YafAssertionException(
            $"Expected {aggregate.GetType().Name} to have raised the domain event '{typeof(TEvent).Name}', but it raised {raised}.");
    }
}

using YAF.Domain;
using YAF.Domain.Events;

namespace YAF.Application.UnitOfWork;

/// <summary>
/// The aggregates one scope changes. Repositories track every aggregate they add, load for change or remove; the unit of work dequeues
/// their domain events after each flush and dispatches them before commit.
/// </summary>
/// <remarks>One instance per scope. It is not thread-safe: a scope runs one unit of work at a time.</remarks>
public interface IAggregateTracker
{
    /// <summary>Gets the tracked aggregates, in the order they were first tracked.</summary>
    IReadOnlyList<IAggregateRoot> Tracked { get; }

    /// <summary>Tracks <paramref name="aggregate"/> for the scope; tracking an aggregate that is already tracked does nothing.</summary>
    /// <param name="aggregate">The aggregate the scope adds, changes or removes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="aggregate"/> is <see langword="null"/>.</exception>
    void Track(IAggregateRoot aggregate);

    /// <summary>
    /// Returns the queued domain events of every tracked aggregate, aggregate by aggregate in tracking order and each aggregate's in the order
    /// they were raised, and empties the aggregates' queues.
    /// </summary>
    /// <returns>The events that were queued; empty when none was.</returns>
    IReadOnlyList<IDomainEvent> DequeueDomainEvents();

    /// <summary>Forgets every tracked aggregate, for example after a rollback.</summary>
    void Clear();
}

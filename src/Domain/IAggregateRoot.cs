using YAF.Core.Results;
using YAF.Domain.Events;

namespace YAF.Domain;

/// <summary>
/// The root of an aggregate, as code outside the domain sees it: the persistence layer collects its queued domain events, and builders
/// and test helpers check its invariants.
/// </summary>
/// <remarks>Derive from <see cref="AggregateRoot{TId, TSelf, TMemento}"/> rather than implementing this interface yourself.</remarks>
public interface IAggregateRoot
{
    /// <summary>Gets the domain events raised since the queue was last emptied, in the order they were raised.</summary>
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>Returns the queued domain events, in the order they were raised, and empties the queue.</summary>
    /// <returns>The events that were queued; empty when none was.</returns>
    IReadOnlyList<IDomainEvent> DequeueDomainEvents();

    /// <summary>Checks the invariants that must hold after every behaviour.</summary>
    /// <returns>Success, or the errors of the invariants that do not hold.</returns>
    Result CheckInvariants();
}

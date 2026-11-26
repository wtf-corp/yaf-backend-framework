using YAF.Core;
using YAF.Core.Ids;
using YAF.Core.Results;
using YAF.Domain.Events;
using YAF.Domain.Mementos;

namespace YAF.Domain;

/// <summary>
/// The base of every aggregate root: an entity that holds the version of the whole aggregate and queues the domain events its
/// behaviours raise, for the persistence layer to collect.
/// </summary>
/// <remarks>
/// The aggregate knows no dispatcher, unit of work or repository: a behaviour calls <see cref="Raise"/> after it has mutated the state.
/// The version belongs to the root; child entities carry none.
/// </remarks>
/// <typeparam name="TId">The aggregate's typed id.</typeparam>
/// <typeparam name="TSelf">The aggregate type itself.</typeparam>
/// <typeparam name="TMemento">The aggregate's memento interface, nested in the aggregate.</typeparam>
public abstract class AggregateRoot<TId, TSelf, TMemento> : Entity<TId, TSelf, TMemento>, IAggregateRoot
    where TId : struct, ITypedId<TId>
    where TSelf : AggregateRoot<TId, TSelf, TMemento>, IOriginator<TSelf, TMemento>
    where TMemento : IEntityMemento, IVersionedMemento
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Initializes a new instance with its id.</summary>
    /// <param name="id">The aggregate's id, assigned by the domain at creation (<c>SessionId.New()</c>) or read from a memento.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is the default of its type.</exception>
    protected AggregateRoot(TId id)
        : base(id)
    {
        DomainEvents = _domainEvents.AsReadOnly();
    }

    /// <summary>
    /// Gets the concurrency token of the whole aggregate, as last read from its memento; <see langword="default"/> when it was never
    /// saved.
    /// </summary>
    public AggregateVersion Version { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <inheritdoc/>
    IReadOnlyList<IDomainEvent> IAggregateRoot.DequeueDomainEvents()
    {
        IDomainEvent[] events = [.. _domainEvents];
        _domainEvents.Clear();
        return events;
    }

    /// <inheritdoc/>
    Result IAggregateRoot.CheckInvariants() => CheckInvariants();

    /// <summary>Queues <paramref name="domainEvent"/> behind the events raised before it.</summary>
    /// <param name="domainEvent">The event, raised inside the behaviour that causes it, after the state has changed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="domainEvent"/> is <see langword="null"/>.</exception>
    protected void Raise(IDomainEvent domainEvent)
    {
        Guard.NotNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>Writes the aggregate's version into <paramref name="memento"/>.</summary>
    /// <param name="memento">The memento to write into.</param>
    private protected override void SnapshotBase(TMemento memento) => memento.Version = Version.Value;

    /// <summary>Reads the aggregate's version from <paramref name="memento"/>.</summary>
    /// <param name="memento">The stored state.</param>
    private protected override void RestoreBase(TMemento memento) => Version = AggregateVersion.From(memento.Version);
}

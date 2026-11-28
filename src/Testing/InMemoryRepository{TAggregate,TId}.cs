using YAF.Application.UnitOfWork;
using YAF.Core.Ids;
using YAF.Domain;

namespace YAF.Testing;

/// <summary>
/// A repository for application tests that keeps aggregate instances in memory, by id. Derive one per aggregate that implements the
/// application's repository interface, as the store's repository does; like that one, it tracks every aggregate it adds, finds, updates or
/// removes, so a <see cref="FakeUnitOfWork"/> given the same tracker publishes their domain events.
/// </summary>
/// <remarks>
/// It keeps the instances themselves, with no snapshot: a change to a found aggregate is visible to the next find without an update, and
/// no version is checked. Adding an id that is stored, or updating or removing one that is not, throws as soon as it is called, where the
/// store would refuse it when the unit of work flushes. The fake is not thread-safe.
/// </remarks>
/// <typeparam name="TAggregate">The aggregate root.</typeparam>
/// <typeparam name="TId">The aggregate's typed id.</typeparam>
/// <example>
/// <code>
/// internal sealed class InMemorySessionRepository(IAggregateTracker tracker)
///     : InMemoryRepository&lt;Session, SessionId&gt;(tracker, session => session.Id), ISessionRepository;
///
/// var repository = new InMemorySessionRepository(tracker);
/// await repository.AddAsync(session, ct);
///
/// var found = await repository.FindAsync(session.Id, ct);
/// </code>
/// </example>
public abstract class InMemoryRepository<TAggregate, TId>
    where TAggregate : class, IAggregateRoot
    where TId : struct, ITypedId<TId>
{
    private readonly IAggregateTracker _tracker;
    private readonly Func<TAggregate, TId> _idOf;
    private readonly Dictionary<TId, TAggregate> _stored = [];

    /// <summary>Creates an empty repository.</summary>
    /// <param name="tracker">Tracks the aggregates the repository adds, finds, updates or removes; usually the one the test's unit of work reads.</param>
    /// <param name="idOf">Returns the id of an aggregate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tracker"/> or <paramref name="idOf"/> is <see langword="null"/>.</exception>
    protected InMemoryRepository(IAggregateTracker tracker, Func<TAggregate, TId> idOf)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(idOf);
        _tracker = tracker;
        _idOf = idOf;
    }

    /// <summary>Gets the stored aggregates, in no particular order.</summary>
    public IReadOnlyCollection<TAggregate> Stored => _stored.Values;

    /// <summary>Returns the aggregate stored with <paramref name="id"/> and tracks it.</summary>
    /// <param name="id">The aggregate's id.</param>
    /// <param name="ct">Not used: the fake completes at once.</param>
    /// <returns>The stored instance, the same on every call until it is replaced or removed; <see langword="null"/> when none is stored with the id.</returns>
    public ValueTask<TAggregate?> FindAsync(TId id, CancellationToken ct)
    {
        if (!_stored.TryGetValue(id, out var aggregate))
        {
            return ValueTask.FromResult<TAggregate?>(null);
        }

        _tracker.Track(aggregate);
        return ValueTask.FromResult<TAggregate?>(aggregate);
    }

    /// <summary>Stores <paramref name="aggregate"/> under its id and tracks it.</summary>
    /// <param name="aggregate">A new aggregate.</param>
    /// <param name="ct">Not used: the fake completes at once.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="aggregate"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">An aggregate is already stored with the id.</exception>
    public ValueTask AddAsync(TAggregate aggregate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var id = _idOf(aggregate);
        if (!_stored.TryAdd(id, aggregate))
        {
            throw new InvalidOperationException($"An aggregate with id {id.Value} is already stored.");
        }

        _tracker.Track(aggregate);
        return ValueTask.CompletedTask;
    }

    /// <summary>Stores <paramref name="aggregate"/> in place of the aggregate stored with its id and tracks it.</summary>
    /// <param name="aggregate">A changed aggregate, usually the instance <see cref="FindAsync"/> returned.</param>
    /// <param name="ct">Not used: the fake completes at once.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="aggregate"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No aggregate is stored with the id.</exception>
    public ValueTask UpdateAsync(TAggregate aggregate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var id = _idOf(aggregate);
        if (!_stored.ContainsKey(id))
        {
            throw NotStored(id);
        }

        _stored[id] = aggregate;
        _tracker.Track(aggregate);
        return ValueTask.CompletedTask;
    }

    /// <summary>Forgets the aggregate stored with the id of <paramref name="aggregate"/> and tracks <paramref name="aggregate"/>.</summary>
    /// <param name="aggregate">The aggregate to remove.</param>
    /// <param name="ct">Not used: the fake completes at once.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="aggregate"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No aggregate is stored with the id.</exception>
    public ValueTask RemoveAsync(TAggregate aggregate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var id = _idOf(aggregate);
        if (!_stored.Remove(id))
        {
            throw NotStored(id);
        }

        _tracker.Track(aggregate);
        return ValueTask.CompletedTask;
    }

    private static InvalidOperationException NotStored(TId id) => new($"No aggregate with id {id.Value} is stored.");
}

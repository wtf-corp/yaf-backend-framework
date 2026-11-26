using YAF.Core;
using YAF.Core.Ids;
using YAF.Core.Results;
using YAF.Domain.Mementos;
using YAF.Domain.Rules;

namespace YAF.Domain;

/// <summary>
/// The base of every entity: a typed id assigned by the domain at creation, a snapshot into and a restore from its memento, and the
/// rule checks a behaviour runs before it mutates anything.
/// </summary>
/// <remarks>
/// A behaviour checks first, then mutates: <c>return Check(new SeatLimitNotExceeded(_seats.Count, Capacity)).Then(() => ...);</c>.
/// A failed behaviour leaves the entity untouched; there is no rollback helper.
/// </remarks>
/// <typeparam name="TId">The entity's typed id.</typeparam>
/// <typeparam name="TSelf">The entity type itself.</typeparam>
/// <typeparam name="TMemento">The entity's memento interface, nested in the entity.</typeparam>
public abstract class Entity<TId, TSelf, TMemento>
    where TId : struct, ITypedId<TId>
    where TSelf : Entity<TId, TSelf, TMemento>, IOriginator<TSelf, TMemento>
    where TMemento : IEntityMemento
{
    /// <summary>Initializes a new instance with its id.</summary>
    /// <param name="id">The entity's id, assigned by the domain at creation (<c>SessionId.New()</c>) or read from a memento.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is the default of its type.</exception>
    protected Entity(TId id)
    {
        Guard.NotDefault(id);
        Id = id;
    }

    /// <summary>Gets the entity's id.</summary>
    public TId Id { get; }

    /// <summary>Writes the entity's whole state into <paramref name="memento"/>: the id first, then <see cref="SnapshotCore"/>.</summary>
    /// <param name="memento">The memento to write into, new or one that was written before.</param>
    /// <exception cref="ArgumentNullException"><paramref name="memento"/> is <see langword="null"/>.</exception>
    public void Snapshot(TMemento memento)
    {
        Guard.NotNull(memento);
        memento.Id = Id.Value;
        SnapshotBase(memento);
        SnapshotCore(memento);
    }

    /// <summary>Writes every member of the entity's own state into <paramref name="memento"/>, every time.</summary>
    /// <param name="memento">The memento to write into; its id is already written.</param>
    protected abstract void SnapshotCore(TMemento memento);

    /// <summary>Reads every member of the entity's own state from <paramref name="memento"/>, without validating it.</summary>
    /// <param name="memento">The stored state, whose id is the entity's.</param>
    protected abstract void RestoreCore(TMemento memento);

    /// <summary>
    /// Restores the entity's state from <paramref name="memento"/>: checks that its id is the entity's, then calls
    /// <see cref="RestoreCore"/>. Call it from <see cref="IOriginator{TSelf, TMemento}.Restore"/>.
    /// </summary>
    /// <param name="memento">The stored state.</param>
    /// <exception cref="ArgumentNullException"><paramref name="memento"/> is <see langword="null"/>.</exception>
    /// <exception cref="CorruptStateException">The memento's id is not the entity's.</exception>
    protected void RestoreFrom(TMemento memento)
    {
        Guard.NotNull(memento);
        if (memento.Id != Id.Value)
        {
            throw new CorruptStateException($"The memento's id {memento.Id} is not the id {Id.Value} of the {typeof(TSelf).Name} it restores.");
        }

        RestoreBase(memento);
        RestoreCore(memento);
    }

    /// <summary>
    /// States the invariants that must hold after every behaviour. Factories and builders call it before they return the entity.
    /// </summary>
    /// <returns>Success, or the errors of the invariants that do not hold.</returns>
    protected virtual Result CheckInvariants() => Result.Success();

    /// <summary>Checks <paramref name="rules"/> in order and stops at the first one that is not satisfied.</summary>
    /// <param name="rules">The rules, evaluated against the current state and the behaviour's inputs.</param>
    /// <returns>Success when every rule holds or there is none; otherwise a failure with the error of the first broken rule.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rules"/> is <see langword="null"/>.</exception>
    protected static Result Check(params IBusinessRule[] rules)
    {
        Guard.NotNull(rules);
        foreach (var rule in rules)
        {
            if (!rule.IsSatisfied())
            {
                return Result.Failure(rule.Error.ToError());
            }
        }

        return Result.Success();
    }

    /// <summary>Checks every one of <paramref name="rules"/>.</summary>
    /// <param name="rules">The rules, evaluated against the current state and the behaviour's inputs.</param>
    /// <returns>Success when every rule holds or there is none; otherwise a failure with the errors of all broken rules, in order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rules"/> is <see langword="null"/>.</exception>
    protected static Result CheckAll(params IBusinessRule[] rules)
    {
        Guard.NotNull(rules);
        List<Error>? errors = null;
        foreach (var rule in rules)
        {
            if (!rule.IsSatisfied())
            {
                (errors ??= []).Add(rule.Error.ToError());
            }
        }

        return errors is null ? Result.Success() : Result.Failure(errors);
    }

    /// <summary>Writes the state the aggregate root adds to every entity's, after the id and before <see cref="SnapshotCore"/>.</summary>
    /// <param name="memento">The memento to write into.</param>
    private protected virtual void SnapshotBase(TMemento memento)
    {
    }

    /// <summary>Reads the state the aggregate root adds to every entity's, after the id check and before <see cref="RestoreCore"/>.</summary>
    /// <param name="memento">The stored state.</param>
    private protected virtual void RestoreBase(TMemento memento)
    {
    }
}

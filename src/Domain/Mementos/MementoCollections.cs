using YAF.Core;
using YAF.Core.Ids;

namespace YAF.Domain.Mementos;

/// <summary>Keeps the collection of child mementos of an aggregate in step with its child entities.</summary>
public static class MementoCollections
{
    /// <summary>
    /// Brings <paramref name="mementos"/> in line with <paramref name="entities"/>, matching them by id: each entity is snapshotted into
    /// the memento with its id, or into a new one from <paramref name="create"/> when there is none; mementos whose id no entity has are
    /// removed. Call it from the parent's <c>SnapshotCore</c>.
    /// </summary>
    /// <remarks>
    /// The mementos that stay keep their order and new ones are appended; the order of <paramref name="entities"/> does not affect the
    /// matching. Matching by position is not offered.
    /// </remarks>
    /// <example>
    /// <code>MementoCollections.Sync(memento.Seats, _seats, memento.NewSeat);</code>
    /// </example>
    /// <typeparam name="TId">The child entities' typed id.</typeparam>
    /// <typeparam name="TEntity">The child entity type.</typeparam>
    /// <typeparam name="TMemento">The child entity's memento interface.</typeparam>
    /// <param name="mementos">The parent memento's collection of child mementos, empty or written before.</param>
    /// <param name="entities">The parent's child entities, each with a different id.</param>
    /// <param name="create">Creates an empty memento for a child that has none, usually the parent memento's factory method.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mementos"/>, <paramref name="entities"/> or <paramref name="create"/> is <see langword="null"/>.</exception>
    public static void Sync<TId, TEntity, TMemento>(
        IList<TMemento> mementos,
        IEnumerable<Entity<TId, TEntity, TMemento>> entities,
        Func<TMemento> create)
        where TId : struct, ITypedId<TId>
        where TEntity : Entity<TId, TEntity, TMemento>, IOriginator<TEntity, TMemento>
        where TMemento : IEntityMemento
    {
        Guard.NotNull(mementos);
        Guard.NotNull(entities);
        Guard.NotNull(create);

        var existing = new Dictionary<Guid, TMemento>();
        foreach (var memento in mementos)
        {
            existing.TryAdd(memento.Id, memento);
        }

        var ids = new HashSet<Guid>();
        foreach (var entity in entities)
        {
            if (existing.TryGetValue(entity.Id.Value, out var memento))
            {
                entity.Snapshot(memento);
            }
            else
            {
                memento = create();
                entity.Snapshot(memento);
                mementos.Add(memento);
            }

            ids.Add(entity.Id.Value);
        }

        for (var i = mementos.Count - 1; i >= 0; i--)
        {
            if (!ids.Contains(mementos[i].Id))
            {
                mementos.RemoveAt(i);
            }
        }
    }
}

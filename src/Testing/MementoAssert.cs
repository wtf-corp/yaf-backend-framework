using System.Collections;
using System.Reflection;
using YAF.Core.Ids;
using YAF.Domain;
using YAF.Domain.Mementos;

namespace YAF.Testing;

/// <summary>
/// Asserts that an entity's hand-written <c>Snapshot</c> and <c>Restore</c> lose nothing: a snapshot, a restore from it and a second
/// snapshot give two mementos that are equal in every member. A failed assertion throws <see cref="YafAssertionException"/>, whose message
/// names the path of the first member that differs and never a value.
/// </summary>
/// <example>
/// <code>
/// var booking = Booking.Create(BookingId.New(), "Workshop");
/// booking.AddLine(BookingLineId.New(), 2);
///
/// MementoAssert.RoundTrips(booking, () => new BookingData());
/// </code>
/// </example>
public static class MementoAssert
{
    /// <summary>
    /// Snapshots <paramref name="entity"/> into a memento from <paramref name="mementoFactory"/>, restores a new entity from it with
    /// <c>TEntity.Restore</c>, snapshots that into a second memento, and compares the two.
    /// </summary>
    /// <remarks>
    /// Every property of <typeparamref name="TMemento"/> and of the interfaces it inherits is compared with <see cref="object.Equals(object, object)"/>.
    /// A property of type <see cref="IList{T}"/> whose items are <see cref="IEntityMemento"/> is compared item by item, matched by
    /// <see cref="IEntityMemento.Id"/>, with the same rule; the path of a member of a child reads <c>Lines[&lt;id&gt;].Quantity</c>.
    /// Give the entity a value in every member, so that a member the restore forgets differs from its default.
    /// </remarks>
    /// <typeparam name="TId">The entity's typed id.</typeparam>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TMemento">The entity's memento interface.</typeparam>
    /// <param name="entity">The entity to round-trip, an aggregate root or a child entity.</param>
    /// <param name="mementoFactory">Creates an empty memento, usually the data class that infrastructure declares for the interface.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entity"/> or <paramref name="mementoFactory"/> is <see langword="null"/>.</exception>
    /// <exception cref="YafAssertionException">A member differs after the round trip; the message names its path.</exception>
    public static void RoundTrips<TId, TEntity, TMemento>(Entity<TId, TEntity, TMemento> entity, Func<TMemento> mementoFactory)
        where TId : struct, ITypedId<TId>
        where TEntity : Entity<TId, TEntity, TMemento>, IOriginator<TEntity, TMemento>
        where TMemento : IEntityMemento
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(mementoFactory);

        var before = mementoFactory();
        entity.Snapshot(before);
        var after = mementoFactory();
        TEntity.Restore(before).Snapshot(after);

        var difference = Compare(typeof(TMemento), before, after, string.Empty);
        if (difference is not null)
        {
            throw new YafAssertionException(
                $"Expected the memento of {typeof(TEntity).Name} to survive Snapshot, Restore and Snapshot unchanged, but {difference}.");
        }
    }

    private static string? Compare(Type mementoType, object before, object after, string path)
    {
        foreach (var property in PropertiesOf(mementoType))
        {
            var memberPath = path + property.Name;
            var beforeValue = property.GetValue(before);
            var afterValue = property.GetValue(after);
            var childType = ChildMementoType(property.PropertyType);
            var difference = childType is not null && beforeValue is not null && afterValue is not null
                ? CompareChildren(childType, (IEnumerable)beforeValue, (IEnumerable)afterValue, memberPath)
                : Equals(beforeValue, afterValue) ? null : $"the member '{memberPath}' differs";
            if (difference is not null)
            {
                return difference;
            }
        }

        return null;
    }

    private static string? CompareChildren(Type childType, IEnumerable before, IEnumerable after, string path)
    {
        var restored = after.Cast<IEntityMemento>().ToList();
        foreach (var child in before.Cast<IEntityMemento>())
        {
            var childPath = $"{path}[{child.Id}]";
            var match = restored.Find(candidate => candidate.Id == child.Id);
            var difference = match is null
                ? $"the child '{childPath}' is missing after the round trip"
                : Compare(childType, child, match, childPath + ".");
            if (difference is not null)
            {
                return difference;
            }

            restored.Remove(match!);
        }

        return restored.Count == 0 ? null : $"the child '{path}[{restored[0].Id}]' was added by the round trip";
    }

    // The memento interface's own properties first, then those of the interfaces it inherits, in a stable order.
    private static IEnumerable<PropertyInfo> PropertiesOf(Type mementoType) =>
        new[] { mementoType }
            .Concat(mementoType.GetInterfaces().OrderBy(type => type.FullName, StringComparer.Ordinal))
            .SelectMany(type => type.GetProperties())
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0);

    private static Type? ChildMementoType(Type propertyType) =>
        propertyType.IsGenericType
        && propertyType.GetGenericTypeDefinition() == typeof(IList<>)
        && typeof(IEntityMemento).IsAssignableFrom(propertyType.GetGenericArguments()[0])
            ? propertyType.GetGenericArguments()[0]
            : null;
}

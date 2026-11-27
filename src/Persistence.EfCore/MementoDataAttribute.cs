namespace YAF.Persistence.EfCore;

/// <summary>
/// Marks a data class whose members the generator emits from the one memento interface the class implements: the base class
/// (<see cref="AggregateData"/> for an aggregate root's memento, <see cref="OwnedEntityData"/> for a child entity's), a settable property
/// for every member of the interface, and the child collections with their factory methods.
/// </summary>
/// <remarks>
/// The class must be <see langword="partial"/> and implement exactly one interface deriving from <see cref="YAF.Domain.Mementos.IEntityMemento"/>.
/// Members that only EF Core needs, such as a navigation or a computed column, go in the class's own partial part; the generator does not
/// emit a member declared there.
/// </remarks>
/// <example>
/// <code language="csharp">
/// [MementoData]
/// internal sealed partial class SessionData : Session.IMemento;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MementoDataAttribute : Attribute;

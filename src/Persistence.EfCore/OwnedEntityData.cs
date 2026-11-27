using YAF.Domain.Mementos;

namespace YAF.Persistence.EfCore;

/// <summary>
/// The base of the data class of a child entity: the members of <see cref="IOwnedEntityMemento"/> (id and stamps) as settable properties
/// that EF Core maps. It has no tenant and no version; those of the aggregate root's row cover it.
/// </summary>
/// <remarks>
/// Do not derive from it by hand: declare a <see langword="partial"/> class marked <see cref="MementoDataAttribute"/> that implements the
/// child entity's memento interface, and the generator derives it from this class and emits the remaining members. The domain fills
/// <see cref="Id"/>; persistence fills the stamps.
/// </remarks>
/// <example>
/// <code language="csharp">
/// [MementoData]
/// internal sealed partial class SeatData : Seat.IMemento;
/// </code>
/// </example>
public abstract class OwnedEntityData : IOwnedEntityMemento
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public Guid CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset ModifiedAt { get; set; }

    /// <inheritdoc />
    public Guid ModifiedBy { get; set; }
}

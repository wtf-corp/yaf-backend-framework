using YAF.Domain.Mementos;

namespace YAF.Persistence.EfCore;

/// <summary>
/// The base of the data class of an aggregate root: the members of <see cref="IAggregateMemento"/> (id, version, tenant and stamps) as
/// settable properties that EF Core maps.
/// </summary>
/// <remarks>
/// Do not derive from it by hand: declare a <see langword="partial"/> class marked <see cref="MementoDataAttribute"/> that implements the
/// aggregate's memento interface, and the generator derives it from this class and emits the remaining members. The domain fills
/// <see cref="Id"/>; persistence fills the version, the tenant and the stamps.
/// </remarks>
/// <example>
/// <code language="csharp">
/// [MementoData]
/// internal sealed partial class SessionData : Session.IMemento;
/// </code>
/// </example>
public abstract class AggregateData : IAggregateMemento
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public Guid Version { get; set; }

    /// <inheritdoc />
    public Guid TenantId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public Guid CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset ModifiedAt { get; set; }

    /// <inheritdoc />
    public Guid ModifiedBy { get; set; }
}

namespace YAF.Domain.Mementos;

/// <summary>
/// The base contract of an aggregate root's memento: its id, its version, its tenant and the stamps of its creation and last
/// modification.
/// </summary>
/// <remarks>The domain fills <see cref="IEntityMemento.Id"/>; persistence fills the version, the tenant and the stamps.</remarks>
/// <example>
/// <code language="csharp">
/// public partial class Session
/// {
///     public interface IMemento : IAggregateMemento
///     {
///         [PublicData] string Title { get; set; }
///     }
/// }
/// </code>
/// </example>
public interface IAggregateMemento : IEntityMemento, IVersionedMemento, ITenantOwnedMemento, IStampedMemento;

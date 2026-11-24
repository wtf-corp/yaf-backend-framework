namespace YAF.Core.Ids;

/// <summary>
/// A typed identifier: a value type that wraps one <see cref="Guid"/> so that the id of one aggregate cannot be passed where another's is
/// expected. Code that handles any typed id, such as logging or persistence, reads the wrapped value through this interface.
/// </summary>
/// <remarks>
/// Do not implement it by hand: declare the id with <see cref="TypedIdAttribute"/> and the generator shipped in <c>YAF.Core</c> implements
/// <see cref="ITypedId{TSelf}"/>. The <c>default</c> value of an id is invalid.
/// </remarks>
public interface ITypedId
{
    /// <summary>Gets the wrapped value, a version 7 UUID assigned when the id was created.</summary>
    Guid Value { get; }
}

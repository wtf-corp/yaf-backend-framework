namespace YAF.Core.Ids;

/// <summary>
/// A typed identifier of type <typeparamref name="TSelf"/> that generic code can create from a stored <see cref="Guid"/>, for example a
/// value converter that applies to every typed id property.
/// </summary>
/// <remarks>
/// Do not implement it by hand: declare the id with <see cref="TypedIdAttribute"/> and the generator shipped in <c>YAF.Core</c> implements it.
/// </remarks>
/// <example>
/// <code language="csharp">
/// static TId Read&lt;TId&gt;(Guid stored) where TId : struct, ITypedId&lt;TId&gt; =&gt; TId.From(stored);
/// </code>
/// </example>
/// <typeparam name="TSelf">The id type itself.</typeparam>
public interface ITypedId<TSelf> : ITypedId
    where TSelf : struct, ITypedId<TSelf>
{
    /// <summary>
    /// Creates the id that wraps <paramref name="value"/>. It restores an id that was created before; new ids are created by the id's
    /// generated <c>New()</c>.
    /// </summary>
    /// <param name="value">The wrapped value, as read from a store or a message.</param>
    /// <returns>The id whose <see cref="ITypedId.Value"/> is <paramref name="value"/>.</returns>
    static abstract TSelf From(Guid value);
}

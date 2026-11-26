namespace YAF.Domain;

/// <summary>
/// The marker base of a value object: an immutable record without an id, compared by value. Deriving from it lets architecture tests find
/// value objects; it adds no behaviour.
/// </summary>
/// <remarks>
/// A value object whose creation can fail has no public constructor and is created through a <c>static Result&lt;T&gt; Create(...)</c>,
/// which validates once. It never holds an id, a tenant or free text with personal data.
/// </remarks>
public abstract record ValueObject;

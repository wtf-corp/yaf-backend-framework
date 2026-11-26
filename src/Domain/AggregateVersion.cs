namespace YAF.Domain;

/// <summary>
/// The concurrency token of a whole aggregate, held by its root. Opaque to the domain: the persistence layer writes a new one each
/// time the aggregate is saved, also when only a child entity changed.
/// </summary>
/// <remarks>The <see langword="default"/> value is the version of an aggregate that was never saved.</remarks>
public readonly record struct AggregateVersion
{
    private AggregateVersion(Guid value) => Value = value;

    /// <summary>Gets the token; <see cref="Guid.Empty"/> for an aggregate that was never saved.</summary>
    public Guid Value { get; }

    /// <summary>Creates the version that a memento or a client holds.</summary>
    /// <param name="value">The stored token.</param>
    /// <returns>The version with <paramref name="value"/> as its token.</returns>
    public static AggregateVersion From(Guid value) => new(value);
}

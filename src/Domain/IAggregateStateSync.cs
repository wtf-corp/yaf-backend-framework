namespace YAF.Domain;

/// <summary>
/// Lets the persistence layer copy the state a save produced back into the aggregate it saved: after each flush the unit of work
/// sets the new version on every tracked aggregate.
/// </summary>
/// <remarks>
/// For the persistence layer only; domain and application code never call it. The version set here is not trusted: the optimistic
/// concurrency check compares with the version the store read, so a forged version makes the next save fail rather than overwrite.
/// <see cref="AggregateRoot{TId, TSelf, TMemento}"/> implements this interface explicitly.
/// </remarks>
public interface IAggregateStateSync
{
    /// <summary>Sets the aggregate's version to the one the store has just saved.</summary>
    /// <param name="version">The version written by the flush that saved the aggregate.</param>
    void SyncVersion(AggregateVersion version);
}

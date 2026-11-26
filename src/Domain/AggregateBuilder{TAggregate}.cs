using YAF.Core.Results;

namespace YAF.Domain;

/// <summary>
/// The base of a builder that creates an aggregate whose creation takes many inputs or has optional parts, and returns it only when its
/// invariants hold.
/// </summary>
/// <remarks>
/// Collect the inputs on the derived builder, create the aggregate in <see cref="Create"/> and queue its "created" event in
/// <see cref="OnCreated"/>, through a method of the aggregate.
/// </remarks>
/// <typeparam name="TAggregate">The aggregate the builder creates.</typeparam>
public abstract class AggregateBuilder<TAggregate>
    where TAggregate : IAggregateRoot
{
    /// <summary>Initializes a new instance.</summary>
    protected AggregateBuilder()
    {
    }

    /// <summary>
    /// Creates the aggregate, checks its invariants and, when they hold, lets <see cref="OnCreated"/> queue the "created" event.
    /// </summary>
    /// <returns>
    /// The aggregate; or the errors of the invariants that do not hold, in which case <see cref="OnCreated"/> is not called.
    /// </returns>
    public Result<TAggregate> Build()
    {
        var aggregate = Create();
        return aggregate.CheckInvariants().Then(() =>
        {
            OnCreated(aggregate);
            return Result.Success(aggregate);
        });
    }

    /// <summary>Creates the aggregate from the inputs collected on the builder, without raising its "created" event.</summary>
    /// <returns>The new aggregate; <see cref="Build"/> checks its invariants.</returns>
    protected abstract TAggregate Create();

    /// <summary>Queues the "created" event, through a method of <paramref name="aggregate"/>.</summary>
    /// <param name="aggregate">The aggregate <see cref="Create"/> returned, whose invariants hold.</param>
    protected abstract void OnCreated(TAggregate aggregate);
}

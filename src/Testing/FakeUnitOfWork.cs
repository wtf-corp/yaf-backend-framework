using YAF.Application.UnitOfWork;
using YAF.Core.Results;

namespace YAF.Testing;

/// <summary>
/// A unit of work for application tests: runs the work and counts what a real one would have committed or rolled back. Given a tracker and
/// a publisher, it also runs the rounds of domain events a real one runs before the commit, and records how many events each round
/// published. It has no transaction: a rollback undoes nothing.
/// </summary>
/// <remarks>
/// A round dequeues the events of the tracked aggregates and publishes them; rounds repeat until one dequeues nothing, at most
/// <c>10</c>. Events still raised after the tenth round fail the work with <see cref="ApplicationErrors.DomainEventDepthExceeded"/>. An
/// exception from the work or a handler propagates and counts neither a commit nor a rollback. The fake is not thread-safe.
/// </remarks>
/// <example>
/// <code>
/// var unitOfWork = new FakeUnitOfWork(tracker, publisher);
///
/// var result = await unitOfWork.ExecuteAsync(token => handler.HandleAsync(command, token), ct);
///
/// unitOfWork.Commits.Should().Be(1);
/// </code>
/// </example>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    private const int MaxRounds = 10;

    private readonly IAggregateTracker? _tracker;
    private readonly IDomainEventPublisher? _publisher;
    private readonly List<int> _rounds = [];

    /// <summary>Creates a unit of work that runs the work only and publishes no domain event.</summary>
    public FakeUnitOfWork()
    {
        Rounds = _rounds.AsReadOnly();
    }

    /// <summary>Creates a unit of work that publishes the domain events of the aggregates <paramref name="tracker"/> tracks.</summary>
    /// <param name="tracker">The aggregates the work changes; usually the one the test's repositories track them in.</param>
    /// <param name="publisher">Runs the handlers of each round's events.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tracker"/> or <paramref name="publisher"/> is <see langword="null"/>.</exception>
    public FakeUnitOfWork(IAggregateTracker tracker, IDomainEventPublisher publisher)
        : this()
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(publisher);
        _tracker = tracker;
        _publisher = publisher;
    }

    /// <summary>Gets how many executions succeeded, with their domain events, and so would have committed.</summary>
    public int Commits { get; private set; }

    /// <summary>Gets how many executions returned a failure, of the work or of a domain event handler, and so would have rolled back.</summary>
    public int Rollbacks { get; private set; }

    /// <summary>
    /// Gets the number of domain events published in each round, of every execution in order; empty without a tracker and a publisher.
    /// </summary>
    public IReadOnlyList<int> Rounds { get; }

    /// <summary>
    /// Runs <paramref name="work"/>, then on success the rounds of domain events, and counts a commit when both succeed or a rollback when
    /// either fails.
    /// </summary>
    /// <typeparam name="TResult">The work's result type: <see cref="Result"/> or a <see cref="Result{T}"/>.</typeparam>
    /// <param name="work">The work, usually a handler or the rest of the request pipeline.</param>
    /// <param name="ct">Passed to the work and the publisher.</param>
    /// <returns>
    /// The result of the work; the failure of the first domain event handler that failed; or
    /// <see cref="ApplicationErrors.DomainEventDepthExceeded"/> when the events kept raising events after the tenth round.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="work"/> is <see langword="null"/>.</exception>
    public async ValueTask<TResult> ExecuteAsync<TResult>(Func<CancellationToken, ValueTask<TResult>> work, CancellationToken ct)
        where TResult : IResult
    {
        ArgumentNullException.ThrowIfNull(work);
        var result = await work(ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            Rollbacks++;
            return result;
        }

        var published = await PublishRoundsAsync(ct).ConfigureAwait(false);
        if (published.IsFailure)
        {
            Rollbacks++;
            return ResultFactory.Failure<TResult>(published.Errors);
        }

        Commits++;
        return result;
    }

    private async ValueTask<Result> PublishRoundsAsync(CancellationToken ct)
    {
        if (_tracker is null || _publisher is null)
        {
            return Result.Success();
        }

        for (var round = 0; ; round++)
        {
            var events = _tracker.DequeueDomainEvents();
            if (events.Count == 0)
            {
                return Result.Success();
            }

            if (round == MaxRounds)
            {
                return ApplicationErrors.DomainEventDepthExceeded.With(("Limit", MaxRounds));
            }

            _rounds.Add(events.Count);
            var published = await _publisher.PublishAsync(events, ct).ConfigureAwait(false);
            if (published.IsFailure)
            {
                return published;
            }
        }
    }
}

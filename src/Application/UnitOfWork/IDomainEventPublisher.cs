using YAF.Core.Results;
using YAF.Domain.Events;

namespace YAF.Application.UnitOfWork;

/// <summary>
/// Publishes domain events to their handlers in process, one after another, inside the caller's transaction. The unit of work calls it with
/// the events it dequeued after each flush; domain events do not go through <see cref="IDispatcher"/>.
/// </summary>
/// <remarks>
/// Each event that derives from <see cref="DomainEvent"/> is stamped with the current time of the <see cref="TimeProvider"/> before its
/// handlers see it. The events run in the order given, and each event's handlers in registration order.
/// </remarks>
public interface IDomainEventPublisher
{
    /// <summary>Runs the handlers of each event in order and stops at the first handler that fails.</summary>
    /// <param name="domainEvents">The events to publish, in the order they were raised.</param>
    /// <param name="ct">Cancels the work; it reaches the handlers.</param>
    /// <returns>
    /// Success when every handler succeeded or no handler is registered; otherwise the result of the first handler that failed.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="domainEvents"/> is <see langword="null"/>.</exception>
    ValueTask<Result> PublishAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct);
}

using YAF.Core.Results;
using YAF.Domain.Events;

namespace YAF.Application;

/// <summary>
/// Reacts to a domain event inside the transaction of the command that raised it, after the changes that raised it were flushed. Domain
/// events do not go through <see cref="IDispatcher"/>.
/// </summary>
/// <typeparam name="TEvent">The domain event type handled.</typeparam>
/// <remarks>
/// A handler has no effect outside the transaction (no HTTP call, no email, no direct publish), because the unit of work may run it again.
/// </remarks>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    /// <summary>Handles <paramref name="domainEvent"/>.</summary>
    /// <param name="domainEvent">The event, with its occurrence time stamped.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <returns>Success, or the errors that roll the whole command back.</returns>
    ValueTask<Result> HandleAsync(TEvent domainEvent, CancellationToken ct);
}

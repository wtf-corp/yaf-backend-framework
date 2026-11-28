using System.Diagnostics;
using YAF.Application.Pipeline;
using YAF.Application.Registration;
using YAF.Core.Results;
using YAF.Domain.Events;

namespace YAF.Application.UnitOfWork;

/// <summary>
/// Stamps the events with one instant of the clock, then hands each event to every registry's <see cref="IHandlerRegistry.TryPublish"/>,
/// whose generated switch runs the event's handlers through the dispatcher in registration order (<c>04</c> §6). Each event runs in an
/// activity <c>yaf.domain_event {EventName}</c> of source <c>YAF.Application</c> (<c>12</c> §4).
/// </summary>
/// <remarks>
/// The status of the activity is <see cref="ActivityStatusCode.Error"/> only for an exception and for a <see cref="ErrorKind.Failure"/> or
/// <see cref="ErrorKind.Unavailable"/> error.
/// </remarks>
internal sealed class DomainEventPublisher(ApplicationRegistrations registrations, Dispatcher runner, TimeProvider timeProvider)
    : IDomainEventPublisher
{
    public async ValueTask<Result> PublishAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);
        var now = timeProvider.GetUtcNow();
        foreach (var raised in domainEvents)
        {
            var domainEvent = raised is DomainEvent stampable ? stampable with { OccurredAt = now } : raised;
            var result = await PublishAsync(domainEvent, ct).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }

    private async ValueTask<Result> PublishAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        using var activity = TelemetryMiddleware.Source.StartActivity("yaf.domain_event", ActivityKind.Internal);
        activity?.DisplayName = $"yaf.domain_event {domainEvent.GetType().Name}";
        try
        {
            foreach (var registry in registrations.Registries)
            {
                if (!registry.TryPublish(domainEvent, runner, ct, out var running))
                {
                    continue;
                }

                var result = await running.ConfigureAwait(false);
                if (result.IsFailure)
                {
                    if (result.Errors.Any(error => error.Kind is ErrorKind.Failure or ErrorKind.Unavailable))
                    {
                        activity?.SetStatus(ActivityStatusCode.Error);
                    }

                    return result;
                }
            }

            return Result.Success();
        }
        catch
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
    }
}

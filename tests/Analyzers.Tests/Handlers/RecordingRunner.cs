using Microsoft.Extensions.DependencyInjection;
using YAF.Application;
using YAF.Application.Registration;
using YAF.Core.Results;
using YAF.Domain.Events;

namespace Analyzers.Tests.Handlers;

/// <summary>
/// Stands in for the dispatcher: runs the handlers that a generated registry added to <paramref name="services"/>, created from their
/// implementation types, and records each call with its static types.
/// </summary>
internal sealed class RecordingRunner(IServiceCollection services) : IRequestRunner
{
    public List<string> Calls { get; } = [];

    public ValueTask<TResult> SendAsync<TRequest, TResult>(TRequest request, CancellationToken ct)
        where TRequest : IRequest<TResult>
    {
        Calls.Add($"send {typeof(TRequest).FullName}");
        return Handlers<IRequestHandler<TRequest, TResult>>().Single().HandleAsync(request, ct);
    }

    public IAsyncEnumerable<TItem> StreamAsync<TQuery, TItem>(TQuery query, CancellationToken ct)
        where TQuery : IStreamQuery<TItem>
    {
        Calls.Add($"stream {typeof(TQuery).FullName}");
        return Handlers<IStreamQueryHandler<TQuery, TItem>>().Single().HandleAsync(query, ct);
    }

    public async ValueTask<Result> PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct)
        where TEvent : IDomainEvent
    {
        Calls.Add($"publish {typeof(TEvent).FullName}");
        foreach (var handler in Handlers<IDomainEventHandler<TEvent>>())
        {
            var result = await handler.HandleAsync(domainEvent, ct).ConfigureAwait(false);
            Calls.Add($"handled by {handler.GetType().Name}");
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }

    private List<THandler> Handlers<THandler>() => services
        .Where(descriptor => descriptor.ServiceType == typeof(THandler))
        .Select(descriptor => (THandler)Activator.CreateInstance(descriptor.ImplementationType!)!)
        .ToList();
}

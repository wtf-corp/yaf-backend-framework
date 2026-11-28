using Microsoft.Extensions.DependencyInjection;
using YAF.Application;
using YAF.Application.Registration;
using YAF.Core.Results;
using YAF.Domain.Events;

namespace Analyzers.Tests.Handlers;

/// <summary>
/// Stands in for the dispatcher: runs the handlers that a generated registry added to <paramref name="services"/>, created from their
/// implementation types, and records each call with its static types. Streams and domain events are not generated yet.
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
        where TQuery : IStreamQuery<TItem> => throw new NotSupportedException();

    public ValueTask<Result> PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct)
        where TEvent : IDomainEvent => throw new NotSupportedException();

    private List<THandler> Handlers<THandler>() => services
        .Where(descriptor => descriptor.ServiceType == typeof(THandler))
        .Select(descriptor => (THandler)Activator.CreateInstance(descriptor.ImplementationType!)!)
        .ToList();
}

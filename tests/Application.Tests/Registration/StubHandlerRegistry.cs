using Microsoft.Extensions.DependencyInjection;
using YAF.Application;
using YAF.Application.Registration;
using YAF.Core.Results;
using YAF.Domain.Events;

[assembly: YafHandlers(typeof(Application.Tests.Registration.StubHandlerRegistry))]

namespace Application.Tests.Registration;

/// <summary>A hand-written stand-in for the generated registry (the generator is T26). Created by the attribute above.</summary>
internal sealed class StubHandlerRegistry : IHandlerRegistry
{
    public StubHandlerRegistry()
        : this([typeof(PingQuery)], [typeof(PingQuery)])
    {
    }

    public StubHandlerRegistry(IReadOnlyCollection<Type> declaredRequests, IReadOnlyCollection<Type> handledRequests)
    {
        DeclaredRequests = declaredRequests;
        HandledRequests = handledRequests;
    }

    public IReadOnlyCollection<Type> DeclaredRequests { get; }

    public IReadOnlyCollection<Type> HandledRequests { get; }

    public void Register(IServiceCollection services) => services.AddSingleton(new PingHandlerStandIn());

    public bool TrySend<TResult>(IRequest<TResult> request, IRequestRunner runner, CancellationToken ct, out ValueTask<TResult> result)
    {
        result = default;
        return false;
    }

    public bool TryStream<TItem>(IStreamQuery<TItem> query, IRequestRunner runner, CancellationToken ct, out IAsyncEnumerable<TItem> items)
    {
        items = AsyncEnumerable.Empty<TItem>();
        return false;
    }

    public bool TryPublish(IDomainEvent domainEvent, IRequestRunner runner, CancellationToken ct, out ValueTask<Result> result)
    {
        result = default;
        return false;
    }
}

/// <summary>Request types the stub declares and handles; only their types are used, so they are abstract.</summary>
internal abstract record PingQuery : IQuery<Result>;

/// <summary>A second request type, declared without a handler in one test.</summary>
internal abstract record PongQuery : IQuery<Result>;

/// <summary>The service the stub registers; not a handler, so that no generator treats this assembly as declaring one.</summary>
internal sealed class PingHandlerStandIn;

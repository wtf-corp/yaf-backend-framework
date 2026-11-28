using Microsoft.Extensions.DependencyInjection;
using YAF.Application;
using YAF.Application.Registration;
using YAF.Core.Results;
using YAF.Domain.Events;

namespace Application.Tests.Registration;

/// <summary>
/// A hand-built stand-in for a generated registry, given to the builder directly. No <see cref="YafHandlersAttribute"/> names it: the
/// attribute allows one per assembly, and that one is the generator's to emit.
/// </summary>
internal sealed class HandBuiltRegistry(IReadOnlyCollection<Type> declaredRequests, IReadOnlyCollection<Type> handledRequests)
    : IHandlerRegistry
{
    public HandBuiltRegistry()
        : this([typeof(PingQuery)], [typeof(PingQuery)])
    {
    }

    public IReadOnlyCollection<Type> DeclaredRequests { get; } = declaredRequests;

    public IReadOnlyCollection<Type> HandledRequests { get; } = handledRequests;

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

/// <summary>Request types the registry declares and handles; only their types are used, so they are abstract.</summary>
internal abstract record PingQuery : IQuery<Result>;

/// <summary>A second request type, declared without a handler in one test.</summary>
internal abstract record PongQuery : IQuery<Result>;

/// <summary>The service the registry registers; not a handler, so that no generator treats this assembly as declaring one.</summary>
internal sealed class PingHandlerStandIn;

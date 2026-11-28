using Microsoft.Extensions.DependencyInjection;
using YAF.Application;
using YAF.Application.Registration;
using YAF.Core.Results;
using YAF.Domain.Events;

namespace Application.Tests.UnitOfWork;

/// <summary>An event with handlers.</summary>
internal sealed record SessionScheduled(int Number) : DomainEvent;

/// <summary>An event no registry handles.</summary>
internal sealed record SessionMoved(int Number) : DomainEvent;

/// <summary>The error the failing handler returns.</summary>
internal abstract class PublishErrors : IErrorCatalog
{
    public static readonly ErrorDescriptor HandlerRefused = Error.Conflict("The handler refused the event.");
}

/// <summary>What the handlers saw, in order.</summary>
internal sealed class HandlerLog
{
    public List<string> Entries { get; } = [];

    public List<IDomainEvent> Received { get; } = [];
}

/// <summary>Records its name and the event's number; fails when <paramref name="fails"/> is set.</summary>
internal sealed class RecordingHandler(HandlerLog log, string name, bool fails = false) : IDomainEventHandler<SessionScheduled>
{
    public ValueTask<Result> HandleAsync(SessionScheduled domainEvent, CancellationToken ct)
    {
        log.Entries.Add($"{name}:{domainEvent.Number}");
        log.Received.Add(domainEvent);
        return ValueTask.FromResult(fails ? Result.Failure(PublishErrors.HandlerRefused.ToError()) : Result.Success());
    }
}

/// <summary>A hand-written stand-in for the generated registry's switch (the generator is T26): it publishes <see cref="SessionScheduled"/>.</summary>
internal sealed class PublishingRegistry : IHandlerRegistry
{
    public IReadOnlyCollection<Type> DeclaredRequests { get; } = [];

    public IReadOnlyCollection<Type> HandledRequests { get; } = [];

    public void Register(IServiceCollection services)
    {
    }

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
        result = domainEvent is SessionScheduled scheduled ? runner.PublishAsync(scheduled, ct) : default;
        return domainEvent is SessionScheduled;
    }
}

/// <summary>Answers only the list of <see cref="SessionScheduled"/> handlers, in the order given.</summary>
internal sealed class HandlerProvider(params IDomainEventHandler<SessionScheduled>[] handlers) : IServiceProvider
{
    public object? GetService(Type serviceType) =>
        serviceType == typeof(IEnumerable<IDomainEventHandler<SessionScheduled>>) ? handlers : null;
}

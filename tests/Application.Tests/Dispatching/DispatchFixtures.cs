using Microsoft.Extensions.DependencyInjection;
using YAF.Application;
using YAF.Application.Pipeline;
using YAF.Application.Registration;
using YAF.Core.Results;
using YAF.Domain.Events;

namespace Application.Tests.Dispatching;

/// <summary>A command; with <see cref="FailOnce"/> its handler throws the first time it runs.</summary>
internal sealed record PingCommand(bool FailOnce = false) : ICommand<Result>;

/// <summary>A query.</summary>
internal sealed record PingQuery : IQuery<Result>;

/// <summary>What ran, in order, and the token the handler saw; scoped, so it also shows which scope each step resolved from.</summary>
internal sealed class CallLog
{
    public List<string> Entries { get; } = [];

    public CancellationToken HandlerToken { get; set; }
}

/// <summary>A hand-written stand-in for the generated registry's switch (the generator is T26); added to the registrations directly.</summary>
internal sealed class DispatchingRegistry : IHandlerRegistry
{
    public IReadOnlyCollection<Type> DeclaredRequests { get; } = [typeof(PingCommand), typeof(PingQuery)];

    public IReadOnlyCollection<Type> HandledRequests { get; } = [typeof(PingCommand), typeof(PingQuery)];

    public void Register(IServiceCollection services)
    {
        services.AddScoped(_ => new CallLog());
        services.AddScoped<IRequestHandler<PingCommand, Result>>(scope => new PingCommandHandler(scope.GetRequiredService<CallLog>()));
        services.AddScoped<IRequestHandler<PingQuery, Result>>(scope => new PingQueryHandler(scope.GetRequiredService<CallLog>()));
    }

    /// <summary>Registers the test middleware with factories, so that the dispatcher resolves them from the scope.</summary>
    public static void RegisterMiddleware(IServiceCollection services)
    {
        services.AddScoped(scope => new OuterMiddleware<PingCommand, Result>(scope.GetRequiredService<CallLog>()));
        services.AddScoped(scope => new OuterMiddleware<PingQuery, Result>(scope.GetRequiredService<CallLog>()));
        services.AddScoped(scope => new InnerMiddleware<PingCommand, Result>(scope.GetRequiredService<CallLog>()));
        services.AddScoped(scope => new InnerMiddleware<PingQuery, Result>(scope.GetRequiredService<CallLog>()));
        services.AddScoped(_ => new ShortCircuitMiddleware());
        services.AddScoped(_ => new RetryMiddleware());
    }

    public bool TrySend<TResult>(IRequest<TResult> request, IRequestRunner runner, CancellationToken ct, out ValueTask<TResult> result)
    {
        result = request switch
        {
            PingCommand command => As<TResult>(runner.SendAsync<PingCommand, Result>(command, ct)),
            PingQuery query => As<TResult>(runner.SendAsync<PingQuery, Result>(query, ct)),
            _ => default,
        };
        return request is PingCommand or PingQuery;
    }

    private static ValueTask<TResult> As<TResult>(ValueTask<Result> running) => (ValueTask<TResult>)(object)running;

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

internal sealed class PingCommandHandler(CallLog log) : IRequestHandler<PingCommand, Result>
{
    public ValueTask<Result> HandleAsync(PingCommand request, CancellationToken ct)
    {
        log.Entries.Add("handler");
        log.HandlerToken = ct;
        return request.FailOnce && log.Entries.Count(entry => entry == "handler") == 1
            ? throw new InvalidOperationException("First attempt fails.")
            : ValueTask.FromResult(Result.Success());
    }
}

internal sealed class PingQueryHandler(CallLog log) : IRequestHandler<PingQuery, Result>
{
    public ValueTask<Result> HandleAsync(PingQuery request, CancellationToken ct)
    {
        log.Entries.Add("handler");
        return ValueTask.FromResult(Result.Success());
    }
}

/// <summary>Records its name before and after the rest of the pipeline.</summary>
internal abstract class RecordingMiddleware<TRequest, TResult>(CallLog log, string name) : IRequestMiddleware<TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    public async ValueTask<TResult> InvokeAsync(TRequest request, RequestDelegate<TResult> next, CancellationToken ct)
    {
        log.Entries.Add($"{name}:before");
        var result = await next(ct).ConfigureAwait(false);
        log.Entries.Add($"{name}:after");
        return result;
    }
}

internal sealed class OuterMiddleware<TRequest, TResult>(CallLog log) : RecordingMiddleware<TRequest, TResult>(log, "outer")
    where TRequest : IRequest<TResult>;

internal sealed class InnerMiddleware<TRequest, TResult>(CallLog log) : RecordingMiddleware<TRequest, TResult>(log, "inner")
    where TRequest : IRequest<TResult>;

/// <summary>Returns without calling the rest of the pipeline.</summary>
internal sealed class ShortCircuitMiddleware : IRequestMiddleware<PingCommand, Result>
{
    public ValueTask<Result> InvokeAsync(PingCommand request, RequestDelegate<Result> next, CancellationToken ct) =>
        ValueTask.FromResult(Result.Success());
}

/// <summary>Catches a failed attempt of the rest of the pipeline and runs it again.</summary>
internal sealed class RetryMiddleware : IRequestMiddleware<PingCommand, Result>
{
    public async ValueTask<Result> InvokeAsync(PingCommand request, RequestDelegate<Result> next, CancellationToken ct)
    {
        try
        {
            return await next(ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return await next(ct).ConfigureAwait(false);
        }
    }
}

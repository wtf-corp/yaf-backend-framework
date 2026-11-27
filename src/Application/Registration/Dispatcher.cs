using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using YAF.Application.Pipeline;
using YAF.Core.Results;

namespace YAF.Application.Registration;

/// <summary>
/// The built-in <see cref="IDispatcher"/>, registered per scope: the registries' generated switch picks the request's static types, and
/// <see cref="IRequestRunner.SendAsync{TRequest, TResult}"/> runs the pipeline steps that apply to the request, outermost first, around its
/// handler. Handler and middleware are resolved from the scope the dispatcher was resolved from (<c>04</c> §5 items 2–4).
/// </summary>
/// <remarks>
/// A stream query runs the "all" and "queries" steps closed over <see cref="StreamQueryRequest{TQuery}"/>. The innermost step enumerates
/// the stream handler and hands each item to the caller, asking the handler for the next one only when the caller does, so a middleware's
/// code after <c>next</c> runs once the caller has asked past the last item (<c>04</c> §5 item 5).
/// </remarks>
internal sealed class Dispatcher(IServiceProvider services, ApplicationRegistrations registrations, IPipelineDescriptor pipeline)
    : IDispatcher, IRequestRunner
{
    public ValueTask<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        foreach (var registry in registrations.Registries)
        {
            if (registry.TrySend(request, this, ct, out var result))
            {
                return result;
            }
        }

        throw new InvalidOperationException($"No registered handler registry handles request {request.GetType().FullName}.");
    }

    public IAsyncEnumerable<TItem> StreamAsync<TItem>(IStreamQuery<TItem> query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        foreach (var registry in registrations.Registries)
        {
            if (registry.TryStream(query, this, ct, out var items))
            {
                return items;
            }
        }

        throw new InvalidOperationException($"No registered handler registry handles stream query {query.GetType().FullName}.");
    }

    ValueTask<TResult> IRequestRunner.SendAsync<TRequest, TResult>(TRequest request, CancellationToken ct)
    {
        var handler = services.GetRequiredService<IRequestHandler<TRequest, TResult>>();
        return Compose(request, token => handler.HandleAsync(request, token))(ct);
    }

    IAsyncEnumerable<TItem> IRequestRunner.StreamAsync<TQuery, TItem>(TQuery query, CancellationToken ct) =>
        StreamThroughPipelineAsync<TQuery, TItem>(query, ct);

    ValueTask<Result> IRequestRunner.PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct) =>
        throw new NotSupportedException("Domain events are not published through the dispatcher yet.");

    private async IAsyncEnumerable<TItem> StreamThroughPipelineAsync<TQuery, TItem>(
        TQuery query,
        [EnumeratorCancellation] CancellationToken ct)
        where TQuery : IStreamQuery<TItem>
    {
        var handler = services.GetRequiredService<IStreamQueryHandler<TQuery, TItem>>();
        var items = Channel.CreateBounded<TItem>(1);
        using var demand = new SemaphoreSlim(0);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var running = RunAsync(
            Compose<StreamQueryRequest<TQuery>, Result>(new StreamQueryRequest<TQuery>(query), async token =>
            {
                await demand.WaitAsync(token).ConfigureAwait(false);
                await foreach (var item in handler.HandleAsync(query, token).WithCancellation(token).ConfigureAwait(false))
                {
                    await items.Writer.WriteAsync(item, token).ConfigureAwait(false);
                    await demand.WaitAsync(token).ConfigureAwait(false);
                }

                return Result.Success();
            }),
            items.Writer,
            stop.Token);
        try
        {
            demand.Release();
            while (await items.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                if (items.Reader.TryRead(out var item))
                {
                    yield return item;
                }

                demand.Release();
            }

            var result = await running.ConfigureAwait(false);
            if (result.IsFailure)
            {
                throw new InvalidOperationException($"The pipeline returned a failure for stream query {typeof(TQuery).FullName}: "
                    + string.Join(", ", result.Errors.Select(error => error.Code)) + ".");
            }
        }
        finally
        {
            if (!running.IsCompleted)
            {
                await stop.CancelAsync().ConfigureAwait(false);
                try
                {
                    await running.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // The caller stopped enumerating early; the pipeline was cancelled on its behalf.
                }
            }
        }
    }

    /// <summary>Runs the pipeline and completes the channel when it ends, so that the caller's enumeration ends too.</summary>
    private static async Task<Result> RunAsync<TItem>(RequestDelegate<Result> pipeline, ChannelWriter<TItem> writer, CancellationToken ct)
    {
        try
        {
            return await pipeline(ct).ConfigureAwait(false);
        }
        finally
        {
            writer.TryComplete();
        }
    }

    /// <summary>Wraps <paramref name="next"/> in the steps that apply to <paramref name="request"/>, outermost first.</summary>
    private RequestDelegate<TResult> Compose<TRequest, TResult>(TRequest request, RequestDelegate<TResult> next)
        where TRequest : IRequest<TResult>
    {
        var steps = pipeline.Steps;
        for (var i = steps.Count - 1; i >= 0; i--)
        {
            var middleware = Middleware<TRequest, TResult>(steps[i], request);
            if (middleware is not null)
            {
                var inner = next;
                next = token => middleware.InvokeAsync(request, inner, token);
            }
        }

        return next;
    }

    /// <summary>The step's middleware from the scope (created with its services when not registered), or <see langword="null"/> when the
    /// step is scoped to the other request kind or is a closed middleware of another request.</summary>
    private IRequestMiddleware<TRequest, TResult>? Middleware<TRequest, TResult>(PipelineStep step, TRequest request)
        where TRequest : IRequest<TResult>
    {
        var applies = step.AppliesTo switch
        {
            PipelineScope.Commands => request is ICommand<TResult>,
            PipelineScope.Queries => request is IQuery<TResult>,
            _ => true,
        };
        if (!applies)
        {
            return null;
        }

        var type = step.Middleware.IsGenericTypeDefinition
            ? step.Middleware.MakeGenericType(typeof(TRequest), typeof(TResult))
            : step.Middleware;
        return typeof(IRequestMiddleware<TRequest, TResult>).IsAssignableFrom(type)
            ? (IRequestMiddleware<TRequest, TResult>)ActivatorUtilities.GetServiceOrCreateInstance(services, type)
            : null;
    }
}

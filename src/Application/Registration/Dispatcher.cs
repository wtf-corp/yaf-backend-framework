using Microsoft.Extensions.DependencyInjection;
using YAF.Application.Pipeline;
using YAF.Core.Results;

namespace YAF.Application.Registration;

/// <summary>
/// The built-in <see cref="IDispatcher"/>, registered per scope: the registries' generated switch picks the request's static types, and
/// <see cref="IRequestRunner.SendAsync{TRequest, TResult}"/> runs the pipeline steps that apply to the request, outermost first, around its
/// handler. Handler and middleware are resolved from the scope the dispatcher was resolved from (<c>04</c> §5 items 2–4).
/// </summary>
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

    public IAsyncEnumerable<TItem> StreamAsync<TItem>(IStreamQuery<TItem> query, CancellationToken ct = default) =>
        throw new NotSupportedException("Stream queries are not dispatched yet.");

    ValueTask<TResult> IRequestRunner.SendAsync<TRequest, TResult>(TRequest request, CancellationToken ct)
    {
        var handler = services.GetRequiredService<IRequestHandler<TRequest, TResult>>();
        RequestDelegate<TResult> next = token => handler.HandleAsync(request, token);

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

        return next(ct);
    }

    IAsyncEnumerable<TItem> IRequestRunner.StreamAsync<TQuery, TItem>(TQuery query, CancellationToken ct) =>
        throw new NotSupportedException("Stream queries are not dispatched yet.");

    ValueTask<Result> IRequestRunner.PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct) =>
        throw new NotSupportedException("Domain events are not published through the dispatcher yet.");

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

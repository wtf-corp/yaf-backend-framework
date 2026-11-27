namespace YAF.Application.Pipeline;

/// <summary>
/// Cross-cutting behaviour that the dispatcher runs around the handler of a request. Register it in order with
/// <see cref="PipelineBuilder"/>; the dispatcher resolves it per request from the caller's scope.
/// </summary>
/// <typeparam name="TRequest">The request the middleware applies to. A middleware for every request is an open generic type.</typeparam>
/// <typeparam name="TResult">The outcome of the request.</typeparam>
/// <remarks>A middleware must be safe to run twice (the unit of work may retry the inner part) and must not swallow a failed result.</remarks>
public interface IRequestMiddleware<in TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    /// <summary>Runs the middleware's behaviour and, unless it short-circuits, the rest of the pipeline through <paramref name="next"/>.</summary>
    /// <param name="request">The request being handled.</param>
    /// <param name="next">The rest of the pipeline, ending in the handler.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The outcome of the request: the result of <paramref name="next"/>, or a failure the middleware returns without calling it.</returns>
    ValueTask<TResult> InvokeAsync(TRequest request, RequestDelegate<TResult> next, CancellationToken ct);
}

namespace YAF.Application.Pipeline;

/// <summary>The rest of the pipeline, ending in the handler, as an <see cref="IRequestMiddleware{TRequest, TResult}"/> sees it.</summary>
/// <typeparam name="TResult">The outcome of the request.</typeparam>
/// <param name="ct">Cancels the request.</param>
/// <returns>The outcome of the rest of the pipeline.</returns>
public delegate ValueTask<TResult> RequestDelegate<TResult>(CancellationToken ct);

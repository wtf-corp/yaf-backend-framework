namespace YAF.Application;

/// <summary>
/// Handles one request type: loads, calls the domain, persists through a repository and returns. The dispatcher runs the pipeline
/// around it; a handler opens no transaction and does not dispatch other requests.
/// </summary>
/// <typeparam name="TRequest">The request type handled.</typeparam>
/// <typeparam name="TResult">The outcome of the request.</typeparam>
/// <remarks>Implement it as an <see langword="internal sealed"/> class; the generated registration adds it to the container.</remarks>
public interface IRequestHandler<in TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    /// <summary>Handles <paramref name="request"/>.</summary>
    /// <param name="request">The request, already validated by the pipeline.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <returns>The outcome. A business failure is a failed result, not an exception.</returns>
    ValueTask<TResult> HandleAsync(TRequest request, CancellationToken ct);
}

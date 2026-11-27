namespace YAF.Application;

/// <summary>
/// Finds the handler for a request and runs the pipeline around it, in the caller's scope. Entry points (endpoints, consumers, jobs)
/// dispatch; request handlers and domain event handlers do not.
/// </summary>
/// <remarks>
/// A missing or duplicate handler fails at build time or at startup, never at first use. An app that must route through another engine
/// registers its own implementation and then keeps these guarantees itself.
/// </remarks>
public interface IDispatcher
{
    /// <summary>Runs the pipeline and the handler of <paramref name="request"/> once.</summary>
    /// <typeparam name="TResult">The outcome of the request.</typeparam>
    /// <param name="request">The command or query.</param>
    /// <param name="ct">Cancels the work; it reaches the handler.</param>
    /// <returns>The outcome of the handler, or of the middleware that short-circuited it.</returns>
    ValueTask<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken ct = default);

    /// <summary>Runs the query pipeline around the handler of <paramref name="query"/> and streams its items.</summary>
    /// <typeparam name="TItem">The type of the streamed items.</typeparam>
    /// <param name="query">The stream query.</param>
    /// <param name="ct">Cancels the enumeration; it reaches the handler.</param>
    /// <returns>The items. The read-only transaction is held until the enumeration ends.</returns>
    IAsyncEnumerable<TItem> StreamAsync<TItem>(IStreamQuery<TItem> query, CancellationToken ct = default);
}

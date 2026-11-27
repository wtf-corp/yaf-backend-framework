using YAF.Core.Results;
using YAF.Domain.Events;

namespace YAF.Application.Registration;

/// <summary>
/// Runs a request, a stream query or a domain event with its static types, so that the generated <see cref="IHandlerRegistry"/> switch
/// calls it without reflection.
/// </summary>
/// <remarks>
/// The dispatcher implements it and passes it to the registries; do not implement. Applications call <see cref="IDispatcher"/>.
/// </remarks>
public interface IRequestRunner
{
    /// <summary>Runs the pipeline around the handler of <paramref name="request"/> in the caller's scope.</summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResult">The outcome of the request.</typeparam>
    /// <param name="request">The command or query.</param>
    /// <param name="ct">Cancels the work; it reaches the handler.</param>
    /// <returns>The outcome of the handler, or of the middleware that short-circuited it.</returns>
    ValueTask<TResult> SendAsync<TRequest, TResult>(TRequest request, CancellationToken ct)
        where TRequest : IRequest<TResult>;

    /// <summary>Runs the query pipeline around the handler of <paramref name="query"/> and streams its items.</summary>
    /// <typeparam name="TQuery">The stream query type.</typeparam>
    /// <typeparam name="TItem">The type of the streamed items.</typeparam>
    /// <param name="query">The stream query.</param>
    /// <param name="ct">Cancels the enumeration; it reaches the handler.</param>
    /// <returns>
    /// The items, handed over one at a time: the handler is asked for the next item only when the caller asks, and a middleware's code
    /// after <c>next</c> runs once the caller has asked past the last item.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// A middleware returned a failure (a stream has no <see cref="Result"/> to return it in); the message names the error codes. Thrown when
    /// the enumeration reaches the end of the items the pipeline let through.
    /// </exception>
    /// <remarks>
    /// The middleware are closed over an internal request that wraps the stream query, not over <typeparamref name="TQuery"/>; a closed
    /// middleware of one request does not run for a stream query.
    /// </remarks>
    IAsyncEnumerable<TItem> StreamAsync<TQuery, TItem>(TQuery query, CancellationToken ct)
        where TQuery : IStreamQuery<TItem>;

    /// <summary>Runs every handler of <paramref name="domainEvent"/> in registration order, stopping at the first failure.</summary>
    /// <typeparam name="TEvent">The domain event type.</typeparam>
    /// <param name="domainEvent">The domain event.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <returns>Success, or the errors of the first handler that failed.</returns>
    ValueTask<Result> PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct)
        where TEvent : IDomainEvent;
}

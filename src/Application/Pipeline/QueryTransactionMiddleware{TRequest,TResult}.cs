using System.Runtime.CompilerServices;
using YAF.Application.Registration;
using YAF.Application.UnitOfWork;
using YAF.Core.Results;

namespace YAF.Application.Pipeline;

/// <summary>
/// The query-transaction step of the default pipeline: runs the rest of the pipeline of a query inside one read-only
/// <see cref="IQueryTransaction"/>, so that the store rejects any write and the handler sees one consistent snapshot.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResult">The request's result type.</typeparam>
/// <remarks>
/// A stream query runs inside <see cref="IQueryTransaction.Stream{TItem}"/>, which holds the transaction until the caller has enumerated
/// the stream or stopped early. A request that is not an <see cref="IQuery{TResult}"/> goes straight to the rest of the pipeline.
/// </remarks>
public sealed class QueryTransactionMiddleware<TRequest, TResult> : IRequestMiddleware<TRequest, TResult>
    where TRequest : IRequest<TResult>
    where TResult : IResult
{
    private readonly IQueryTransaction _transaction;

    /// <summary>Creates the middleware; the dispatcher creates it per request from the scope.</summary>
    /// <param name="transaction">The query transaction of the request's scope.</param>
    public QueryTransactionMiddleware(IQueryTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        _transaction = transaction;
    }

    /// <summary>Runs the rest of the pipeline inside the query transaction when the request is a query.</summary>
    /// <param name="request">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The result of the query transaction for a query; the result of <paramref name="next"/> for any other request.</returns>
    public ValueTask<TResult> InvokeAsync(TRequest request, RequestDelegate<TResult> next, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(next);
        return request switch
        {
            IStreamQueryRequest => _transaction.Stream(token => OnceAsync(next, token), ct).FirstAsync(ct),
            IQuery<TResult> => _transaction.ExecuteAsync(token => next(token), ct),
            _ => next(ct),
        };
    }

    /// <summary>The result of the rest of the pipeline as a stream of one item; for a stream query it arrives once the stream has ended.</summary>
    private static async IAsyncEnumerable<TResult> OnceAsync(RequestDelegate<TResult> next, [EnumeratorCancellation] CancellationToken ct)
    {
        yield return await next(ct).ConfigureAwait(false);
    }
}

using YAF.Core.Results;

namespace YAF.Application.UnitOfWork;

/// <summary>
/// Runs a query's work in one <c>READ ONLY</c> transaction: the store rejects any write, and the work sees one consistent snapshot.
/// <see cref="Pipeline.QueryTransactionMiddleware{TRequest, TResult}"/> runs every query through it.
/// </summary>
/// <remarks>The transaction always ends, also when the work fails or throws; it is never committed with a change.</remarks>
public interface IQueryTransaction
{
    /// <summary>Runs <paramref name="work"/> in a read-only transaction and ends the transaction when the work returns.</summary>
    /// <typeparam name="TResult">The work's result type.</typeparam>
    /// <param name="work">The work, usually the rest of the request pipeline.</param>
    /// <param name="ct">Cancels the work and the transaction.</param>
    /// <returns>The result of the work.</returns>
    ValueTask<TResult> ExecuteAsync<TResult>(Func<CancellationToken, ValueTask<TResult>> work, CancellationToken ct)
        where TResult : IResult;

    /// <summary>
    /// Runs the stream <paramref name="work"/> returns in a read-only transaction that begins when the enumeration starts and ends when
    /// the enumerator is disposed, so that the transaction is held while the stream is enumerated.
    /// </summary>
    /// <typeparam name="TItem">The stream's item type.</typeparam>
    /// <param name="work">Starts the stream.</param>
    /// <param name="ct">Cancels the stream and the transaction.</param>
    /// <returns>The items of the stream.</returns>
    IAsyncEnumerable<TItem> Stream<TItem>(Func<CancellationToken, IAsyncEnumerable<TItem>> work, CancellationToken ct);
}

namespace YAF.Application;

/// <summary>Handles one stream query type: yields its items while the query pipeline holds the read-only transaction.</summary>
/// <typeparam name="TQuery">The stream query type handled.</typeparam>
/// <typeparam name="TItem">The type of the streamed items.</typeparam>
/// <remarks>Implement it as an <see langword="internal sealed"/> class; the generated registration adds it to the container.</remarks>
public interface IStreamQueryHandler<in TQuery, out TItem>
{
    /// <summary>Streams the items that answer <paramref name="query"/>.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">Cancels the enumeration.</param>
    /// <returns>The items, read only while the caller enumerates them.</returns>
    IAsyncEnumerable<TItem> HandleAsync(TQuery query, CancellationToken ct);
}

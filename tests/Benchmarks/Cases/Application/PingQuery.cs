using YAF.Application;
using YAF.Core.Results;

namespace Benchmarks.Cases.Application;

/// <summary>The query <see cref="DispatcherBenchmarks"/> sends; its handler is in this assembly's generated handler registry.</summary>
/// <param name="Value">What the handler returns.</param>
internal sealed record PingQuery(int Value) : IQuery<Result<int>>;

/// <summary>Returns the query's value at once, so that a benchmark measures the call and not the handler.</summary>
internal sealed class PingQueryHandler : IRequestHandler<PingQuery, Result<int>>
{
    public ValueTask<Result<int>> HandleAsync(PingQuery request, CancellationToken ct) => ValueTask.FromResult(Result.Success(request.Value));
}

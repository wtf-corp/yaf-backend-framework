using BenchmarkDotNet.Attributes;
using YAF.Core.Results;

namespace Benchmarks.Cases.Results;

/// <summary>Shows that a successful <see cref="Result{T}"/> round trip allocates nothing (03 §5 item 4).</summary>
[MemoryDiagnoser]
public class ResultBenchmarks
{
    private readonly int _input = 1;

    /// <summary>Success, then Map, Then and Match, as in <c>ResultAllocationTests</c> of Core.Tests.</summary>
    /// <returns>The outcome of the round trip, so that it is not optimised away.</returns>
    [Benchmark]
    public int SuccessRoundTrip() =>
        Result.Success(_input)
            .Map(static value => value + 1)
            .Then(static value => Result.Success(value * 2))
            .Match(static value => value, static _ => -1);
}

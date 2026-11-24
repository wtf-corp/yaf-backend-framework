using YAF.Core.Results;

namespace Core.Tests.Results;

public sealed class ResultAllocationTests
{
    [Fact]
    public void SuccessRoundTrip_AllocatesNothing()
    {
        RoundTrip(1);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var outcome = RoundTrip(2);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        outcome.Should().Be(6);
        allocated.Should().Be(0);
    }

    private static int RoundTrip(int input) =>
        Result.Success(input)
            .Map(static value => value + 1)
            .Then(static value => Result.Success(value * 2))
            .Match(static value => value, static _ => -1);
}

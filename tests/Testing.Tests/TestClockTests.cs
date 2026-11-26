using YAF.Testing;

namespace Testing.Tests;

public sealed class TestClockTests
{
    private static readonly DateTimeOffset s_start = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Default_StartsAtTheFixedInstant()
    {
        var clock = new TestClock();

        clock.Now.Should().Be(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    #region test-clock
    [Fact]
    public void Advance_ByAnHour_MovesNowAndTheProvider()
    {
        var clock = new TestClock(s_start);

        clock.Advance(TimeSpan.FromHours(1));

        clock.Now.Should().Be(s_start.AddHours(1));
        clock.TimeProvider.GetUtcNow().Should().Be(s_start.AddHours(1));
    }
    #endregion

    [Fact]
    public void SetNow_Later_MovesNow()
    {
        var clock = new TestClock(s_start);
        var later = s_start.AddDays(3);

        clock.SetNow(later);

        clock.Now.Should().Be(later);
    }

    [Fact]
    public void TimeProvider_GetUtcNow_EqualsNow()
    {
        var clock = new TestClock(s_start);

        clock.TimeProvider.GetUtcNow().Should().Be(clock.Now);
    }
}

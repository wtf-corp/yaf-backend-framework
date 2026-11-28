using Testing.Tests.Fixtures;
using YAF.Testing;

namespace Testing.Tests;

public sealed class TestIdsTests
{
    private static readonly long s_timestampBase = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    [Fact]
    public void SameSeed_GivesTheSameSequence()
    {
        var first = new TestIds(7);
        var second = new TestIds(7);

        Guid[] fromFirst = [first.NextGuid(), first.NextGuid(), first.NextGuid()];
        Guid[] fromSecond = [second.NextGuid(), second.NextGuid(), second.NextGuid()];

        fromFirst.Should().Equal(fromSecond);
    }

    [Fact]
    public void DifferentSeeds_GiveDifferentIds()
    {
        var one = new TestIds(1);
        var two = new TestIds(2);

        var fromOne = one.NextGuid();
        var fromTwo = two.NextGuid();

        fromOne.Should().NotBe(fromTwo);
    }

    [Fact]
    public void Next_IsAVersion7Uuid()
    {
        var ids = new TestIds();

        var text = ids.NextGuid().ToString("D");

        // RFC 9562 §4.1, §4.2, §5.7: version 0b0111 in the first digit of the third group, variant 0b10 in the first of the fourth.
        text.Should().MatchRegex("^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$");
    }

    [Fact]
    public void NextGuid_Twice_CountsMillisecondsFromTheTimestampBase()
    {
        var ids = new TestIds();

        Guid[] issued = [ids.NextGuid(), ids.NextGuid()];

        issued.Select(UnixMilliseconds).Should().Equal(s_timestampBase, s_timestampBase + 1);
    }

    [Fact]
    public void Next_TypedId_WrapsTheNextGuid()
    {
        var reference = new TestIds(3);
        reference.NextGuid();
        var ids = new TestIds(3);
        ids.NextGuid();

        var id = ids.Next<BookingId>();

        id.Value.Should().Be(reference.NextGuid());
    }

    // RFC 9562 §5.7: unix_ts_ms is the first 48 bits, the first twelve hexadecimal digits of the text form.
    private static long UnixMilliseconds(Guid id) => Convert.ToInt64(id.ToString("N")[..12], 16);
}

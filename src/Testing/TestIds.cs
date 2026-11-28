using System.Buffers.Binary;
using YAF.Core.Ids;

namespace YAF.Testing;

/// <summary>
/// Deterministic ids for tests: version 7 UUIDs (RFC 9562) that are the same on every run, so a test can name the ids it expects and a
/// failing test prints the same ids each time. Two instances with the same seed give the same sequence.
/// </summary>
/// <remarks>
/// Each id carries the Unix timestamp of 2026-01-01T00:00:00Z, where a <see cref="TestClock"/> starts by default, plus one millisecond per id
/// the instance gave before it. So the ids of one instance ascend in the order they were given, as ids that a typed id's <c>New()</c> creates
/// one after another do. The seed fills the last 32 bits and the other random bits are zero, so ids of different seeds never coincide. The
/// ids are not random: use them in tests only. The instance is not thread-safe.
/// </remarks>
/// <example>
/// <code>
/// var ids = new TestIds();
///
/// var context = FakeExecutionContext.ForUser(ids.Next&lt;UserId&gt;(), ids.Next&lt;TenantId&gt;());
/// </code>
/// </example>
public sealed class TestIds
{
    // 2026-01-01T00:00:00Z in Unix milliseconds, the default start of TestClock.
    private const long TimestampBase = 1_767_225_600_000;

    private readonly int _seed;
    private long _issued;

    /// <summary>Creates a sequence of ids that depends on <paramref name="seed"/> alone.</summary>
    /// <param name="seed">Selects the sequence; any value. Instances with the same seed give the same ids in the same order.</param>
    public TestIds(int seed = 1)
    {
        _seed = seed;
    }

    /// <summary>Returns the next id of the sequence.</summary>
    /// <returns>A version 7 UUID whose timestamp is one millisecond later than that of the id before it.</returns>
    public Guid NextGuid()
    {
        // RFC 9562 §5.7, as two big-endian 64-bit halves: unix_ts_ms (48 bits), ver 0b0111, rand_a zero; var 0b10, rand_b the seed.
        var unixMilliseconds = TimestampBase + _issued++;
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteInt64BigEndian(bytes, (unixMilliseconds << 16) | 0x7000);
        BinaryPrimitives.WriteUInt64BigEndian(bytes[8..], 0x8000_0000_0000_0000UL | (uint)_seed);
        return new Guid(bytes, bigEndian: true);
    }

    /// <summary>Returns a typed id that wraps the next id of the sequence, the one <see cref="NextGuid"/> would have returned.</summary>
    /// <typeparam name="TId">The typed id, declared with <see cref="TypedIdAttribute"/>.</typeparam>
    /// <returns>The id whose <see cref="ITypedId.Value"/> is the next id of the sequence.</returns>
    public TId Next<TId>()
        where TId : struct, ITypedId<TId> => TId.From(NextGuid());
}

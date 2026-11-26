using Microsoft.Extensions.Time.Testing;

namespace YAF.Testing;

/// <summary>
/// A clock a test moves by hand. Domain code takes time as a value, so a domain test passes <see cref="Now"/>; code that takes a
/// <see cref="System.TimeProvider"/> gets <see cref="TimeProvider"/>, which always reports the same instant as <see cref="Now"/>.
/// </summary>
/// <remarks>Time never moves by itself and never moves backwards.</remarks>
/// <example>
/// <code>
/// var clock = new TestClock();
/// clock.Advance(TimeSpan.FromHours(1));
/// var period = Period.Create(clock.Now, clock.Now.AddDays(1));
/// </code>
/// </example>
public sealed class TestClock
{
    private readonly FakeTimeProvider _provider;

    /// <summary>Initializes a new instance that starts at 2026-01-01T00:00:00Z.</summary>
    public TestClock()
        : this(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
    {
    }

    /// <summary>Initializes a new instance that starts at <paramref name="start"/>.</summary>
    /// <param name="start">The instant <see cref="Now"/> reports until the clock is moved.</param>
    public TestClock(DateTimeOffset start)
    {
        _provider = new FakeTimeProvider(start);
    }

    /// <summary>Gets the time provider to pass to code that reads the time; it reports <see cref="Now"/> and moves with this clock.</summary>
    public TimeProvider TimeProvider => _provider;

    /// <summary>Gets the current instant, in UTC.</summary>
    public DateTimeOffset Now => _provider.GetUtcNow();

    /// <summary>Moves the clock forward by <paramref name="by"/>.</summary>
    /// <param name="by">How far to move; zero leaves the clock where it is.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="by"/> is negative.</exception>
    public void Advance(TimeSpan by) => _provider.Advance(by);

    /// <summary>Moves the clock forward to <paramref name="now"/>.</summary>
    /// <param name="now">The new current instant; not earlier than <see cref="Now"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="now"/> is earlier than <see cref="Now"/>.</exception>
    public void SetNow(DateTimeOffset now) => _provider.SetUtcNow(now);
}

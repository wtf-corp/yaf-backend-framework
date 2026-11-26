using YAF.Core.Results;

namespace YAF.Domain.ValueObjects;

/// <summary>
/// A span of time between two instants that includes its <see cref="Start"/> and excludes its <see cref="End"/>, so that a period
/// ending at ten and one starting at ten do not overlap.
/// </summary>
/// <remarks>
/// There is no public constructor: create a period with <see cref="Create"/>. Instants are compared as points in time, whatever their
/// offset. A period whose two ends are equal is empty: it contains nothing and overlaps nothing. For calendar days, with both ends
/// included, use <see cref="DateRange"/>.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var start = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
/// var talk = Period.Create(start, start.AddMinutes(45)).Value;
/// bool clash = talk.Overlaps(Period.Create(start.AddMinutes(45), start.AddMinutes(90)).Value); // false
/// </code>
/// </example>
public sealed record Period : ValueObject
{
    private Period(DateTimeOffset start, DateTimeOffset end)
    {
        Start = start;
        End = end;
    }

    /// <summary>Gets the instant the period starts, which it includes.</summary>
    public DateTimeOffset Start { get; }

    /// <summary>Gets the instant the period ends, which it excludes.</summary>
    public DateTimeOffset End { get; }

    /// <summary>Creates a period.</summary>
    /// <param name="start">The first instant, included.</param>
    /// <param name="end">The end, excluded; the same instant as <paramref name="start"/> (an empty period) or later.</param>
    /// <returns>
    /// The period; or <see cref="ValueObjectErrors.PeriodEndBeforeStart"/> when <paramref name="end"/> is before
    /// <paramref name="start"/>.
    /// </returns>
    public static Result<Period> Create(DateTimeOffset start, DateTimeOffset end) =>
        end >= start ? new Period(start, end) : ValueObjectErrors.PeriodEndBeforeStart;

    /// <summary>Tells whether an instant lies in the period.</summary>
    /// <param name="instant">The instant.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="instant"/> is <see cref="Start"/> or later and before <see cref="End"/>; always
    /// <see langword="false"/> for an empty period.
    /// </returns>
    public bool Contains(DateTimeOffset instant) => instant >= Start && instant < End;

    /// <summary>Tells whether two periods share at least one instant.</summary>
    /// <param name="other">The other period.</param>
    /// <returns>
    /// <see langword="true"/> when an instant lies in both periods; periods where one ends as the other starts do not overlap, and an
    /// empty period overlaps nothing.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public bool Overlaps(Period other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Start < End && other.Start < other.End && Start < other.End && other.Start < End;
    }

    /// <summary>Computes the instants two periods share.</summary>
    /// <param name="other">The other period.</param>
    /// <returns>The shared period; or <see langword="null"/> when the periods do not <see cref="Overlaps">overlap</see>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public Period? Intersect(Period other) =>
        Overlaps(other) ? new Period(Max(Start, other.Start), Min(End, other.End)) : null;

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) => left >= right ? left : right;

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;
}

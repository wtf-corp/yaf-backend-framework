using YAF.Core.Results;

namespace YAF.Domain.ValueObjects;

/// <summary>
/// A range of calendar days that includes both its <see cref="Start"/> and its <see cref="End"/>: a range from 1 to 3 November holds
/// three days, and one whose two ends are equal holds one day.
/// </summary>
/// <remarks>
/// There is no public constructor: create a range with <see cref="Create"/>. For instants, with the end excluded, use
/// <see cref="Period"/>.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var conference = DateRange.Create(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 4)).Value;
/// bool lastDay = conference.Contains(new DateOnly(2026, 11, 4)); // true
/// </code>
/// </example>
public sealed record DateRange : ValueObject
{
    private DateRange(DateOnly start, DateOnly end)
    {
        Start = start;
        End = end;
    }

    /// <summary>Gets the first day of the range, which it includes.</summary>
    public DateOnly Start { get; }

    /// <summary>Gets the last day of the range, which it includes.</summary>
    public DateOnly End { get; }

    /// <summary>Creates a range of days.</summary>
    /// <param name="start">The first day, included.</param>
    /// <param name="end">The last day, included; the same as <paramref name="start"/> or later.</param>
    /// <returns>
    /// The range; or <see cref="ValueObjectErrors.DateRangeEndBeforeStart"/> when <paramref name="end"/> is before
    /// <paramref name="start"/>.
    /// </returns>
    public static Result<DateRange> Create(DateOnly start, DateOnly end) =>
        end >= start ? new DateRange(start, end) : ValueObjectErrors.DateRangeEndBeforeStart;

    /// <summary>Tells whether a day lies in the range.</summary>
    /// <param name="date">The day.</param>
    /// <returns><see langword="true"/> when <paramref name="date"/> is from <see cref="Start"/> to <see cref="End"/>, both included.</returns>
    public bool Contains(DateOnly date) => date >= Start && date <= End;

    /// <summary>Tells whether two ranges share at least one day.</summary>
    /// <param name="other">The other range.</param>
    /// <returns>
    /// <see langword="true"/> when a day lies in both ranges; a range that ends the day before the other starts does not overlap it.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public bool Overlaps(DateRange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Start <= other.End && other.Start <= End;
    }

    /// <summary>Computes the days two ranges share.</summary>
    /// <param name="other">The other range.</param>
    /// <returns>The shared days; or <see langword="null"/> when the ranges do not <see cref="Overlaps">overlap</see>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public DateRange? Intersect(DateRange other) =>
        Overlaps(other) ? new DateRange(Max(Start, other.Start), Min(End, other.End)) : null;

    private static DateOnly Max(DateOnly left, DateOnly right) => left >= right ? left : right;

    private static DateOnly Min(DateOnly left, DateOnly right) => left <= right ? left : right;
}

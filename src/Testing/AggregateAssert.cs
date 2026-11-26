using YAF.Domain;

namespace YAF.Testing;

/// <summary>
/// Asserts that the invariants of an aggregate hold, for example after a behaviour or a restore. A failed assertion throws
/// <see cref="YafAssertionException"/>, whose message lists the error codes of the broken invariants and never an error message.
/// </summary>
/// <example>
/// <code>
/// booking.AddLine(lineId, 2);
///
/// AggregateAssert.InvariantsHold(booking);
/// </code>
/// </example>
public static class AggregateAssert
{
    /// <summary>Asserts that <see cref="IAggregateRoot.CheckInvariants"/> of <paramref name="aggregate"/> returns success.</summary>
    /// <param name="aggregate">The aggregate to check.</param>
    /// <exception cref="ArgumentNullException"><paramref name="aggregate"/> is <see langword="null"/>.</exception>
    /// <exception cref="YafAssertionException">An invariant does not hold; the message lists the error codes it returned.</exception>
    public static void InvariantsHold(IAggregateRoot aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        var result = aggregate.CheckInvariants();
        if (result.IsFailure)
        {
            throw new YafAssertionException(
                $"Expected the invariants of {aggregate.GetType().Name} to hold, but they failed with the error codes "
                + $"{string.Join(", ", result.Errors.Select(error => $"'{error.Code}'"))}.");
        }
    }
}

using Testing.Tests.Fixtures;
using YAF.Testing;

namespace Testing.Tests;

public sealed class AggregateAssertTests
{
    private const string Title = "Workshop booking of attendee-0001@example.test";

    private static readonly Guid s_dataSubject = Guid.CreateVersion7();

    #region aggregate-assert
    [Fact]
    public void InvariantsHold_ValidAggregate_Passes()
    {
        var booking = Booking.Create(BookingId.New(), Title, s_dataSubject);
        booking.AddLine(BookingLineId.New(), 2);

        var act = () => AggregateAssert.InvariantsHold(booking);

        act.Should().NotThrow();
    }
    #endregion

    [Fact]
    public void InvariantsHold_BrokenInvariant_ThrowsListingTheCodes()
    {
        var booking = Booking.Create(BookingId.New(), string.Empty, s_dataSubject);
        booking.AddLine(BookingLineId.New(), 0);

        var act = () => AggregateAssert.InvariantsHold(booking);

        act.Should().Throw<YafAssertionException>()
            .Which.Message.Should().Contain($"'{BookingErrors.TitleMissing.Code}'")
            .And.Contain($"'{BookingErrors.QuantityNotPositive.Code}'")
            .And.Contain(nameof(Booking))
            .And.NotContain("needs a title");
    }
}

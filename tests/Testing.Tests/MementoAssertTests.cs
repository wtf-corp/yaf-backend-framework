using Testing.Tests.Fixtures;
using YAF.Testing;

namespace Testing.Tests;

public sealed class MementoAssertTests
{
    private const string Title = "Workshop booking of attendee-0001@example.test";

    private static readonly Guid s_dataSubject = Guid.CreateVersion7();

    #region memento-round-trip
    [Fact]
    public void RoundTrips_LosslessAggregate_Passes()
    {
        var booking = Booking.Create(BookingId.New(), Title, s_dataSubject);
        booking.AddLine(BookingLineId.New(), 2);
        booking.AddLine(BookingLineId.New(), 3);

        var act = () => MementoAssert.RoundTrips(booking, () => new BookingData());

        act.Should().NotThrow();
    }
    #endregion

    [Fact]
    public void RoundTrips_RestoreForgetsAMember_ThrowsNamingTheMember()
    {
        var booking = Booking.Create(BookingId.New(), Title, s_dataSubject, BookingFlaw.ForgetsTitle);

        var act = () => MementoAssert.RoundTrips(booking, () => new BookingData());

        act.Should().Throw<YafAssertionException>()
            .Which.Message.Should().Contain("'Title'").And.Contain(nameof(Booking));
    }

    [Fact]
    public void RoundTrips_ChildLost_ThrowsNamingTheCollection()
    {
        var booking = Booking.Create(BookingId.New(), Title, s_dataSubject, BookingFlaw.LosesLastLine);
        var lost = BookingLineId.New();
        booking.AddLine(BookingLineId.New(), 2);
        booking.AddLine(lost, 3);

        var act = () => MementoAssert.RoundTrips(booking, () => new BookingData());

        act.Should().Throw<YafAssertionException>()
            .Which.Message.Should().Contain($"'Lines[{lost.Value}]'").And.Contain("missing");
    }

    [Fact]
    public void RoundTrips_ChildMemberDiffers_ThrowsWithThePath()
    {
        var booking = Booking.Create(BookingId.New(), Title, s_dataSubject, BookingFlaw.ForgetsLineQuantity);
        var line = BookingLineId.New();
        booking.AddLine(line, 3);

        var act = () => MementoAssert.RoundTrips(booking, () => new BookingData());

        act.Should().Throw<YafAssertionException>()
            .Which.Message.Should().Contain($"'Lines[{line.Value}].Quantity'");
    }

    [Fact]
    public void RoundTrips_InheritedMember_IsCompared()
    {
        var booking = Booking.Create(BookingId.New(), Title, s_dataSubject, BookingFlaw.ForgetsDataSubject);

        var act = () => MementoAssert.RoundTrips(booking, () => new BookingData());

        act.Should().Throw<YafAssertionException>()
            .Which.Message.Should().Contain("'DataSubjectId'");
    }

    [Fact]
    public void Message_NeverContainsAValue()
    {
        var booking = Booking.Create(BookingId.New(), Title, s_dataSubject, BookingFlaw.ForgetsTitle);

        var act = () => MementoAssert.RoundTrips(booking, () => new BookingData());

        act.Should().Throw<YafAssertionException>()
            .Which.Message.Should().NotContain(Title).And.NotContain(s_dataSubject.ToString());
    }

    [Fact]
    public void RoundTrips_NullEntity_Throws()
    {
        var act = () => MementoAssert.RoundTrips<BookingId, Booking, Booking.IMemento>(null!, () => new BookingData());

        act.Should().Throw<ArgumentNullException>();
    }
}

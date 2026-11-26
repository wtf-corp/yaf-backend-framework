using Testing.Tests.Fixtures;
using YAF.Domain;
using YAF.Testing;

namespace Testing.Tests;

public sealed class DomainEventAssertTests
{
    private const string Title = "Workshop booking of attendee-0001@example.test";

    private static readonly Guid s_dataSubject = Guid.CreateVersion7();

    #region domain-event-assert
    [Fact]
    public void Raised_QueuedEvent_ReturnsItAndKeepsTheQueue()
    {
        var booking = Booking.Create(BookingId.New(), Title, s_dataSubject);
        var first = BookingLineId.New();
        booking.Confirm();
        booking.AddLine(first, 2);
        booking.AddLine(BookingLineId.New(), 3);

        var raised = DomainEventAssert.Raised<BookingLineAdded>(booking);

        raised.LineId.Should().Be(first);
        ((IAggregateRoot)booking).DomainEvents.Should().HaveCount(3);
    }
    #endregion

    [Fact]
    public void Raised_NoSuchEvent_ThrowsListingTheRaisedTypes()
    {
        var booking = Booking.Create(BookingId.New(), Title, s_dataSubject);
        booking.AddLine(BookingLineId.New(), 2);

        var act = () => DomainEventAssert.Raised<BookingConfirmed>(booking);

        act.Should().Throw<YafAssertionException>()
            .Which.Message.Should().Contain($"'{nameof(BookingConfirmed)}'")
            .And.Contain($"'{nameof(BookingLineAdded)}'")
            .And.Contain(nameof(Booking));
    }

    [Fact]
    public void Raised_NoEventAtAll_ThrowsSayingSo()
    {
        var booking = Booking.Create(BookingId.New(), Title, s_dataSubject);

        var act = () => DomainEventAssert.Raised<BookingLineAdded>(booking);

        act.Should().Throw<YafAssertionException>().Which.Message.Should().Contain("no domain event");
    }

    [Fact]
    public void Message_NeverContainsAValue()
    {
        var booking = Booking.Create(BookingId.New(), Title, s_dataSubject);
        var line = BookingLineId.New();
        booking.AddLine(line, 2);

        var act = () => DomainEventAssert.Raised<BookingConfirmed>(booking);

        act.Should().Throw<YafAssertionException>()
            .Which.Message.Should().NotContain(line.Value.ToString()).And.NotContain(Title);
    }
}

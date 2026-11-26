namespace Testing.Tests.Fixtures;

// Stands in for the data class that infrastructure declares for the memento.
internal sealed class BookingData : Booking.IMemento
{
    public Guid Id { get; set; }

    public Guid Version { get; set; }

    public Guid TenantId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset ModifiedAt { get; set; }

    public Guid ModifiedBy { get; set; }

    public Guid DataSubjectId { get; set; }

    public string Title { get; set; } = string.Empty;

    public BookingFlaw Flaw { get; set; }

    public IList<BookingLine.IMemento> Lines { get; } = [];

    public BookingLine.IMemento NewLine() => new BookingLineData();
}

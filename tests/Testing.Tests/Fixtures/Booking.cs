using YAF.Core.Classification;
using YAF.Domain;
using YAF.Domain.Mementos;

namespace Testing.Tests.Fixtures;

internal sealed class Booking : AggregateRoot<BookingId, Booking, Booking.IMemento>, IOriginator<Booking, Booking.IMemento>
{
    private readonly List<BookingLine> _lines = [];

    private Booking(BookingId id)
        : base(id)
    {
    }

    internal interface IMemento : IAggregateMemento, IDataSubjectOwnedMemento
    {
        [PublicData]
        string Title { get; set; }

        BookingFlaw Flaw { get; set; }

        IList<BookingLine.IMemento> Lines { get; }

        BookingLine.IMemento NewLine();
    }

    public string Title { get; private set; } = string.Empty;

    public Guid DataSubject { get; private set; }

    public BookingFlaw Flaw { get; private set; }

    public static Booking Create(BookingId id, string title, Guid dataSubject, BookingFlaw flaw = BookingFlaw.None) =>
        new(id) { Title = title, DataSubject = dataSubject, Flaw = flaw };

    public static Booking Restore(IMemento memento)
    {
        var booking = new Booking(BookingId.From(memento.Id));
        booking.RestoreFrom(memento);
        return booking;
    }

    public void AddLine(BookingLineId lineId, int quantity) => _lines.Add(BookingLine.Create(lineId, quantity));

    protected override void SnapshotCore(IMemento memento)
    {
        memento.Title = Title;
        memento.DataSubjectId = DataSubject;
        memento.Flaw = Flaw;
        MementoCollections.Sync(memento.Lines, _lines, memento.NewLine);
    }

    protected override void RestoreCore(IMemento memento)
    {
        Flaw = memento.Flaw;
        Title = Flaw == BookingFlaw.ForgetsTitle ? string.Empty : memento.Title;
        DataSubject = Flaw == BookingFlaw.ForgetsDataSubject ? Guid.Empty : memento.DataSubjectId;
        _lines.Clear();
        _lines.AddRange(memento.Lines.Select(line =>
            BookingLine.Restore(Flaw == BookingFlaw.ForgetsLineQuantity ? new BookingLineData { Id = line.Id } : line)));
        if (Flaw == BookingFlaw.LosesLastLine)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
    }
}

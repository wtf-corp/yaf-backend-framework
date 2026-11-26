using YAF.Domain;
using YAF.Domain.Mementos;

namespace Testing.Tests.Fixtures;

internal sealed class BookingLine : Entity<BookingLineId, BookingLine, BookingLine.IMemento>, IOriginator<BookingLine, BookingLine.IMemento>
{
    private BookingLine(BookingLineId id)
        : base(id)
    {
    }

    internal interface IMemento : IOwnedEntityMemento
    {
        int Quantity { get; set; }
    }

    public int Quantity { get; private set; }

    public static BookingLine Create(BookingLineId id, int quantity) => new(id) { Quantity = quantity };

    public static BookingLine Restore(IMemento memento)
    {
        var line = new BookingLine(BookingLineId.From(memento.Id));
        line.RestoreFrom(memento);
        return line;
    }

    protected override void SnapshotCore(IMemento memento) => memento.Quantity = Quantity;

    protected override void RestoreCore(IMemento memento) => Quantity = memento.Quantity;
}

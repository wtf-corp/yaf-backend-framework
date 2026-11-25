namespace Domain.Tests.Fixtures;

// Stands in for the data class that infrastructure declares for the memento.
internal sealed class OrderLineData : OrderLine.IMemento
{
    public Guid Id { get; set; }

    public int Quantity { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset ModifiedAt { get; set; }

    public Guid ModifiedBy { get; set; }
}

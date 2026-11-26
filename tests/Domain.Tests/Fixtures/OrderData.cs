namespace Domain.Tests.Fixtures;

// Stands in for the data class that infrastructure declares for the memento.
internal sealed class OrderData : Order.IMemento
{
    public Guid Id { get; set; }

    public Guid Version { get; set; }

    public Guid TenantId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset ModifiedAt { get; set; }

    public Guid ModifiedBy { get; set; }

    public string Title { get; set; } = string.Empty;

    public IList<OrderLine.IMemento> Lines { get; } = [];

    public OrderLine.IMemento NewLine() => new OrderLineData();
}

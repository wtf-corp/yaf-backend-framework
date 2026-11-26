using YAF.Domain.ValueObjects;

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

    public CustomerId CustomerId { get; set; }

    public OrderStatus Status { get; set; }

    // Filled by Snapshot, or by the store before Restore reads it.
    public Channel Channel { get; set; } = null!;

    public Percentage Discount { get; set; } = null!;

    public EmailAddress? ContactEmail { get; set; }

    public Money Deposit { get; set; } = null!;

    public Period? DeliveryWindow { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public CustomerId? ReferredBy { get; set; }

    public IList<OrderLine.IMemento> Lines { get; } = [];

    public OrderLine.IMemento NewLine() => new OrderLineData();
}

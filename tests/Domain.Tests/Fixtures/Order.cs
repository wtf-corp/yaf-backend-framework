using YAF.Core.Classification;
using YAF.Core.Results;
using YAF.Domain;
using YAF.Domain.Mementos;
using YAF.Domain.ValueObjects;

namespace Domain.Tests.Fixtures;

#region aggregate
internal sealed class Order : AggregateRoot<OrderId, Order, Order.IMemento>, IOriginator<Order, Order.IMemento>
{
    private static readonly Money s_noDeposit = new(0m, Currency.FromCode("EUR").Value);

    private readonly List<OrderLine> _lines = [];

    private Order(OrderId id)
        : base(id)
    {
    }

    internal interface IMemento : IAggregateMemento
    {
        [PublicData]
        string Title { get; set; }

        // One member of every allowed type (02 §5): typed id, enum, smart enumeration, single- and multi-value value objects, and
        // their nullable forms.
        CustomerId CustomerId { get; set; }

        OrderStatus Status { get; set; }

        Channel Channel { get; set; }

        Percentage Discount { get; set; }

        EmailAddress? ContactEmail { get; set; }

        Money Deposit { get; set; }

        Period? DeliveryWindow { get; set; }

        DateTimeOffset? ConfirmedAt { get; set; }

        CustomerId? ReferredBy { get; set; }

        IList<OrderLine.IMemento> Lines { get; }

        OrderLine.IMemento NewLine();
    }

    public string Title { get; private set; } = string.Empty;

    public CustomerId CustomerId { get; private set; }

    public OrderStatus Status { get; private set; }

    public Channel Channel { get; private set; } = Channel.Web;

    public Percentage Discount { get; private set; } = Percentage.Create(0m).Value;

    public EmailAddress? ContactEmail { get; private set; }

    public Money Deposit { get; private set; } = s_noDeposit;

    public Period? DeliveryWindow { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public CustomerId? ReferredBy { get; private set; }

    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    public static Result<Order> Create(OrderId id, string title) => Result.Success(new Order(id) { Title = title });

    // The start of creation through OrderBuilder, which checks the invariants.
    internal static Order Draft(OrderId id, string title) => new(id) { Title = title };

    public static Order Restore(IMemento memento)
    {
        var order = new Order(OrderId.From(memento.Id));
        order.RestoreFrom(memento);
        return order;
    }

    #region behaviour
    public Result AddLine(OrderLineId lineId, int quantity)
    {
        var line = OrderLine.Create(lineId, quantity);
        if (line.IsFailure)
        {
            return Result.Failure(line.Errors);
        }

        _lines.Add(line.Value);
        Raise(new OrderLineAdded(Id, lineId, quantity));
        return Result.Success();
    }
    #endregion

    public void RemoveLine(OrderLineId lineId) => _lines.RemoveAll(line => line.Id == lineId);

    // Sets the members that the memento round trip covers.
    internal void Amend(
        CustomerId customerId,
        OrderStatus status,
        Channel channel,
        Percentage discount,
        EmailAddress? contactEmail,
        Money deposit,
        Period? deliveryWindow,
        DateTimeOffset? confirmedAt,
        CustomerId? referredBy)
    {
        CustomerId = customerId;
        Status = status;
        Channel = channel;
        Discount = discount;
        ContactEmail = contactEmail;
        Deposit = deposit;
        DeliveryWindow = deliveryWindow;
        ConfirmedAt = confirmedAt;
        ReferredBy = referredBy;
    }

    // Queues the "created" event; OrderBuilder calls it once the invariants hold.
    internal void Place() => Raise(new OrderPlaced(Id));

    protected override Result CheckInvariants() =>
        CheckAll([new TitleRequired(Title), .. _lines.Select(line => new QuantityPositive(line.Quantity))]);

    #region sync-children
    protected override void SnapshotCore(IMemento memento)
    {
        memento.Title = Title;
        memento.CustomerId = CustomerId;
        memento.Status = Status;
        memento.Channel = Channel;
        memento.Discount = Discount;
        memento.ContactEmail = ContactEmail;
        memento.Deposit = Deposit;
        memento.DeliveryWindow = DeliveryWindow;
        memento.ConfirmedAt = ConfirmedAt;
        memento.ReferredBy = ReferredBy;
        MementoCollections.Sync(memento.Lines, _lines, memento.NewLine);
    }
    #endregion

    protected override void RestoreCore(IMemento memento)
    {
        Title = memento.Title;
        CustomerId = memento.CustomerId;
        Status = memento.Status;
        Channel = memento.Channel;
        Discount = memento.Discount;
        ContactEmail = memento.ContactEmail;
        Deposit = memento.Deposit;
        DeliveryWindow = memento.DeliveryWindow;
        ConfirmedAt = memento.ConfirmedAt;
        ReferredBy = memento.ReferredBy;
        _lines.Clear();
        _lines.AddRange(memento.Lines.Select(OrderLine.Restore));
    }
}
#endregion

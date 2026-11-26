using YAF.Core.Classification;
using YAF.Core.Results;
using YAF.Domain;
using YAF.Domain.Mementos;

namespace Domain.Tests.Fixtures;

#region aggregate
internal sealed class Order : AggregateRoot<OrderId, Order, Order.IMemento>, IOriginator<Order, Order.IMemento>
{
    private readonly List<OrderLine> _lines = [];

    private Order(OrderId id)
        : base(id)
    {
    }

    internal interface IMemento : IAggregateMemento
    {
        [PublicData]
        string Title { get; set; }

        IList<OrderLine.IMemento> Lines { get; }

        OrderLine.IMemento NewLine();
    }

    public string Title { get; private set; } = string.Empty;

    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    public static Result<Order> Create(OrderId id, string title) => Result.Success(new Order(id) { Title = title });

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

    protected override Result CheckInvariants() => CheckAll([.. _lines.Select(line => new QuantityPositive(line.Quantity))]);

    #region sync-children
    protected override void SnapshotCore(IMemento memento)
    {
        memento.Title = Title;
        MementoCollections.Sync(memento.Lines, _lines, memento.NewLine);
    }
    #endregion

    protected override void RestoreCore(IMemento memento)
    {
        Title = memento.Title;
        _lines.Clear();
        _lines.AddRange(memento.Lines.Select(OrderLine.Restore));
    }
}
#endregion

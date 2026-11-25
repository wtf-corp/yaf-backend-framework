using YAF.Core.Results;
using YAF.Domain;
using YAF.Domain.Mementos;

namespace Domain.Tests.Fixtures;

#region child-entity
internal sealed class OrderLine : Entity<OrderLineId, OrderLine, OrderLine.IMemento>, IOriginator<OrderLine, OrderLine.IMemento>
{
    private OrderLine(OrderLineId id)
        : base(id)
    {
    }

    internal interface IMemento : IOwnedEntityMemento
    {
        int Quantity { get; set; }
    }

    public int Quantity { get; private set; }

    public static Result<OrderLine> Create(OrderLineId id, int quantity) =>
        Check(new QuantityPositive(quantity))
            .Then(() => Result.Success(new OrderLine(id) { Quantity = quantity }));

    public static OrderLine Restore(IMemento memento)
    {
        var line = new OrderLine(OrderLineId.From(memento.Id));
        line.RestoreFrom(memento);
        return line;
    }

    public Result ChangeQuantity(int quantity) =>
        Check(new QuantityPositive(quantity))
            .Then(() => { Quantity = quantity; });

    protected override void SnapshotCore(IMemento memento) => memento.Quantity = Quantity;

    protected override void RestoreCore(IMemento memento) => Quantity = memento.Quantity;
}
#endregion

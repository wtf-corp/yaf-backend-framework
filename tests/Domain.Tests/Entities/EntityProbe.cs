using Domain.Tests.Fixtures;
using YAF.Core.Results;
using YAF.Domain;
using YAF.Domain.Rules;

namespace Domain.Tests.Entities;

// Reaches the protected members of the entity base that a real entity calls only from inside.
internal sealed class EntityProbe : Entity<OrderLineId, EntityProbe, OrderLine.IMemento>, IOriginator<EntityProbe, OrderLine.IMemento>
{
    public EntityProbe(OrderLineId id)
        : base(id)
    {
    }

    public bool CoreRestored { get; private set; }

    public static EntityProbe Restore(OrderLine.IMemento memento)
    {
        var probe = new EntityProbe(OrderLineId.From(memento.Id));
        probe.RestoreFrom(memento);
        return probe;
    }

    public static Result RunCheck(params IBusinessRule[] rules) => Check(rules);

    public static Result RunCheckAll(params IBusinessRule[] rules) => CheckAll(rules);

    public void Reload(OrderLine.IMemento memento) => RestoreFrom(memento);

    protected override void SnapshotCore(OrderLine.IMemento memento)
    {
    }

    protected override void RestoreCore(OrderLine.IMemento memento) => CoreRestored = true;
}

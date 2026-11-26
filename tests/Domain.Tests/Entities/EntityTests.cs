using Domain.Tests.Fixtures;
using YAF.Domain.Mementos;

namespace Domain.Tests.Entities;

public sealed class EntityTests
{
    [Fact]
    public void Snapshot_WritesIdThenState()
    {
        var line = OrderLine.Create(OrderLineId.New(), 3).Value;
        var memento = new OrderLineData();

        line.Snapshot(memento);

        memento.Id.Should().Be(line.Id.Value);
        memento.Quantity.Should().Be(3);
    }

    [Fact]
    public void Restore_Memento_ReadsIdAndState()
    {
        var memento = new OrderLineData { Id = OrderLineId.New().Value, Quantity = 4 };

        var line = OrderLine.Restore(memento);

        line.Id.Value.Should().Be(memento.Id);
        line.Quantity.Should().Be(4);
    }

    [Fact]
    public void Constructor_DefaultId_Throws()
    {
        var act = () => OrderLine.Create(default, 1);

        act.Should().Throw<ArgumentException>().WithParameterName("id");
    }

    [Fact]
    public void RestoreFrom_ForeignId_ThrowsCorruptState()
    {
        var probe = new EntityProbe(OrderLineId.New());
        var foreign = new OrderLineData { Id = OrderLineId.New().Value };

        var act = () => probe.Reload(foreign);

        act.Should().Throw<CorruptStateException>();
        probe.CoreRestored.Should().BeFalse();
    }

    [Fact]
    public void Check_FirstBrokenRule_ReturnsItsError()
    {
        var result = EntityProbe.RunCheck(new QuantityPositive(1), new QuantityPositive(0), new QuantityWithinLimit(5, 3));

        result.Errors.Select(e => e.Code).Should().Equal(OrderErrors.QuantityNotPositive.Code);
    }

    [Fact]
    public void Check_NoRule_Succeeds()
    {
        var result = EntityProbe.RunCheck();

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CheckAll_TwoBrokenRules_ReturnsBothInOrder()
    {
        var result = EntityProbe.RunCheckAll(new QuantityWithinLimit(5, 3), new QuantityPositive(1), new QuantityPositive(0));

        result.Errors.Select(e => e.Code).Should().Equal(OrderErrors.QuantityAboveLimit.Code, OrderErrors.QuantityNotPositive.Code);
    }

    [Fact]
    public void CheckAll_NoRule_Succeeds()
    {
        var result = EntityProbe.RunCheckAll();

        result.IsSuccess.Should().BeTrue();
    }

    #region check-then-mutate
    [Fact]
    public void Check_ThenMutation_RunsOnlyOnSuccess()
    {
        var line = OrderLine.Create(OrderLineId.New(), 2).Value;

        var result = line.ChangeQuantity(0);

        result.Errors.Select(e => e.Code).Should().Equal(OrderErrors.QuantityNotPositive.Code);
        line.Quantity.Should().Be(2);
    }
    #endregion

    [Fact]
    public void ChangeQuantity_KeptRule_Mutates()
    {
        var line = OrderLine.Create(OrderLineId.New(), 2).Value;

        var result = line.ChangeQuantity(5);

        result.IsSuccess.Should().BeTrue();
        line.Quantity.Should().Be(5);
    }
}

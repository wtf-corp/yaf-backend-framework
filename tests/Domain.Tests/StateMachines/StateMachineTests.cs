using Domain.Tests.Fixtures;
using YAF.Domain.StateMachines;

namespace Domain.Tests.StateMachines;

public sealed class StateMachineTests
{
    [Fact]
    public void Firing_UndefinedTransition_FailsWithTheOtherwiseError()
    {
        var result = OrderLifecycle.Machine.Fire(OrderStatus.Cancelled, OrderTrigger.Place);

        result.Errors.Select(e => e.Code).Should().Equal(OrderErrors.TransitionNotAllowed.Code);
        result.Errors[0].Arguments.Should().Contain("From", OrderStatus.Cancelled).And.Contain("Trigger", OrderTrigger.Place);
    }

    [Fact]
    public void Firing_DefinedTransition_ReturnsTheTarget()
    {
        var result = OrderLifecycle.Machine.Fire(OrderStatus.Draft, OrderTrigger.Place);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(OrderStatus.Placed);
    }

    [Fact]
    public void Cancel_PlacedOrder_IsCancelled()
    {
        var order = OrderIn(OrderStatus.Placed);

        var result = order.Cancel();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_CancelledOrder_FailsWithTransitionNotAllowed()
    {
        var order = OrderIn(OrderStatus.Cancelled);

        var result = order.Cancel();

        result.Errors.Select(e => e.Code).Should().Equal(OrderErrors.TransitionNotAllowed.Code);
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void PermittedTriggers_ListsInDefinitionOrder()
    {
        var triggers = OrderLifecycle.Machine.PermittedTriggers(OrderStatus.Draft);

        triggers.Should().Equal(OrderTrigger.Cancel, OrderTrigger.Place);
    }

    [Fact]
    public void PermittedTriggers_StateWithoutTransitions_IsEmpty()
    {
        var triggers = OrderLifecycle.Machine.PermittedTriggers(OrderStatus.Cancelled);

        triggers.Should().BeEmpty();
    }

    [Fact]
    public void States_ListsEveryNamedState()
    {
        var states = OrderLifecycle.Machine.States;

        states.Should().Equal(OrderStatus.Draft, OrderStatus.Cancelled, OrderStatus.Placed);
    }

    [Fact]
    public void Define_DuplicateTransition_Throws()
    {
        var define = () => StateMachine<OrderStatus, OrderTrigger>.Define(b => b
            .From(OrderStatus.Draft).On(OrderTrigger.Place).To(OrderStatus.Placed)
            .From(OrderStatus.Draft).On(OrderTrigger.Place).To(OrderStatus.Cancelled)
            .Otherwise(OrderErrors.TransitionNotAllowed));

        define.Should().Throw<InvalidOperationException>().WithMessage("*Draft*Place*");
    }

    [Fact]
    public void Define_OnWithoutFrom_Throws()
    {
        var define = () => StateMachine<OrderStatus, OrderTrigger>.Define(b => b
            .On(OrderTrigger.Place).To(OrderStatus.Placed)
            .Otherwise(OrderErrors.TransitionNotAllowed));

        define.Should().Throw<InvalidOperationException>().WithMessage("*Place*");
    }

    [Fact]
    public void Define_ToWithoutOn_Throws()
    {
        var define = () => StateMachine<OrderStatus, OrderTrigger>.Define(b => b
            .From(OrderStatus.Draft).To(OrderStatus.Placed)
            .Otherwise(OrderErrors.TransitionNotAllowed));

        define.Should().Throw<InvalidOperationException>().WithMessage("*Draft*");
    }

    [Fact]
    public void Define_OtherwiseAfterUnfinishedFrom_Throws()
    {
        var define = () => StateMachine<OrderStatus, OrderTrigger>.Define(b => b
            .From(OrderStatus.Draft).On(OrderTrigger.Place).To(OrderStatus.Placed)
            .From(OrderStatus.Placed).On(OrderTrigger.Cancel)
            .Otherwise(OrderErrors.TransitionNotAllowed));

        define.Should().Throw<InvalidOperationException>().WithMessage("*Placed*");
    }

    [Fact]
    public void Firing_SmartEnumerationStates_Works()
    {
        var machine = StateMachine<Channel, OrderTrigger>.Define(b => b
            .From(Channel.Web).On(OrderTrigger.Reopen).To(Channel.Phone)
            .From(Channel.Phone).On(OrderTrigger.Reopen).To(Channel.Store)
            .Otherwise(OrderErrors.TransitionNotAllowed));

        var result = machine.Fire(Channel.Web, OrderTrigger.Reopen);

        result.Value.Should().BeSameAs(Channel.Phone);
        machine.States.Should().Equal(Channel.Web, Channel.Phone, Channel.Store);
        machine.Fire(Channel.Store, OrderTrigger.Reopen).Errors.Select(e => e.Code).Should().Equal(OrderErrors.TransitionNotAllowed.Code);
    }

    private static Order OrderIn(OrderStatus status) => Order.Restore(new OrderData { Id = OrderId.New().Value, Status = status });
}

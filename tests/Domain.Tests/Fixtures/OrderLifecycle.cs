using YAF.Domain.StateMachines;

namespace Domain.Tests.Fixtures;

internal static class OrderLifecycle
{
    #region state-machine
    public static readonly StateMachine<OrderStatus, OrderTrigger> Machine =
        StateMachine<OrderStatus, OrderTrigger>.Define(b => b
            .From(OrderStatus.Draft).On(OrderTrigger.Cancel).To(OrderStatus.Cancelled)
            .From(OrderStatus.Draft).On(OrderTrigger.Place).To(OrderStatus.Placed)
            .From(OrderStatus.Placed).On(OrderTrigger.Cancel).To(OrderStatus.Cancelled)
            .Otherwise(OrderErrors.TransitionNotAllowed));
    #endregion
}

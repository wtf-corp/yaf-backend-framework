using Domain.Tests.Fixtures;
using YAF.Domain.StateMachines;

namespace Domain.Tests.StateMachines;

public sealed class StateMachineMermaidTests
{
    #region mermaid
    [Fact]
    public void ToMermaid_ThreeTransitions_WritesHeaderAndOneLineEach()
    {
        var diagram = OrderLifecycle.Machine.ToMermaid();

        diagram.Should().Be(
            "stateDiagram-v2\n" +
            "    Draft --> Cancelled: Cancel\n" +
            "    Draft --> Placed: Place\n" +
            "    Placed --> Cancelled: Cancel\n");
    }
    #endregion

    [Fact]
    public void ToMermaid_SelfTransition_IsWritten()
    {
        var machine = StateMachine<OrderStatus, OrderTrigger>.Define(b => b
            .From(OrderStatus.Placed).On(OrderTrigger.Reopen).To(OrderStatus.Placed)
            .Otherwise(OrderErrors.TransitionNotAllowed));

        var diagram = machine.ToMermaid();

        diagram.Should().Be("stateDiagram-v2\n    Placed --> Placed: Reopen\n");
    }
}

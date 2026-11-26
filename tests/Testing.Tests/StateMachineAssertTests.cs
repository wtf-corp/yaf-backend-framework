using YAF.Core.Results;
using YAF.Domain.StateMachines;
using YAF.Testing;

namespace Testing.Tests;

public sealed class StateMachineAssertTests
{
    private static readonly SessionStatus[] s_finals = [SessionStatus.Cancelled, SessionStatus.Completed];

    #region state-machine-assert
    [Fact]
    public void IsWellFormed_WellFormedMachine_Passes()
    {
        var lifecycle = StateMachine<SessionStatus, SessionTrigger>.Define(b => b
            .From(SessionStatus.Draft).On(SessionTrigger.Publish).To(SessionStatus.Published)
            .From(SessionStatus.Draft).On(SessionTrigger.Cancel).To(SessionStatus.Cancelled)
            .From(SessionStatus.Published).On(SessionTrigger.Cancel).To(SessionStatus.Cancelled)
            .From(SessionStatus.Published).On(SessionTrigger.Hold).To(SessionStatus.Completed)
            .Otherwise(SessionErrors.TransitionNotAllowed));

        var act = () => StateMachineAssert.IsWellFormed(lifecycle, SessionStatus.Draft, s_finals);

        act.Should().NotThrow();
    }
    #endregion

    [Fact]
    public void IsWellFormed_UnreachableState_ThrowsNamingIt()
    {
        var lifecycle = StateMachine<SessionStatus, SessionTrigger>.Define(b => b
            .From(SessionStatus.Draft).On(SessionTrigger.Publish).To(SessionStatus.Published)
            .From(SessionStatus.Published).On(SessionTrigger.Cancel).To(SessionStatus.Cancelled)
            .From(SessionStatus.Completed).On(SessionTrigger.Cancel).To(SessionStatus.Cancelled)
            .Otherwise(SessionErrors.TransitionNotAllowed));

        var act = () => StateMachineAssert.IsWellFormed(lifecycle, SessionStatus.Draft, s_finals);

        act.Should().Throw<YafAssertionException>()
            .Which.Message.Should().Contain("unreachable")
            .And.Contain($"'{SessionStatus.Completed}'")
            .And.NotContain($"'{SessionStatus.Published}'");
    }

    [Fact]
    public void IsWellFormed_NonFinalStateWithoutExit_ThrowsNamingIt()
    {
        var lifecycle = StateMachine<SessionStatus, SessionTrigger>.Define(b => b
            .From(SessionStatus.Draft).On(SessionTrigger.Publish).To(SessionStatus.Published)
            .From(SessionStatus.Draft).On(SessionTrigger.Cancel).To(SessionStatus.Cancelled)
            .From(SessionStatus.Draft).On(SessionTrigger.Hold).To(SessionStatus.Completed)
            .Otherwise(SessionErrors.TransitionNotAllowed));

        var act = () => StateMachineAssert.IsWellFormed(lifecycle, SessionStatus.Draft, s_finals);

        act.Should().Throw<YafAssertionException>()
            .Which.Message.Should().Contain("no exit")
            .And.Contain($"'{SessionStatus.Published}'")
            .And.NotContain("unreachable")
            .And.NotContain(nameof(SessionTrigger.Hold));
    }

    [Fact]
    public void IsWellFormed_SelfLoopOnly_IsNotAnExit()
    {
        var lifecycle = StateMachine<SessionStatus, SessionTrigger>.Define(b => b
            .From(SessionStatus.Draft).On(SessionTrigger.Publish).To(SessionStatus.Published)
            .From(SessionStatus.Draft).On(SessionTrigger.Cancel).To(SessionStatus.Cancelled)
            .From(SessionStatus.Draft).On(SessionTrigger.Hold).To(SessionStatus.Completed)
            .From(SessionStatus.Published).On(SessionTrigger.Hold).To(SessionStatus.Published)
            .Otherwise(SessionErrors.TransitionNotAllowed));

        var act = () => StateMachineAssert.IsWellFormed(lifecycle, SessionStatus.Draft, s_finals);

        act.Should().Throw<YafAssertionException>()
            .Which.Message.Should().Contain("no exit")
            .And.Contain($"'{SessionStatus.Published}'");
    }

    [Fact]
    public void IsWellFormed_EnumValueNotInTheDefinition_ThrowsNamingIt()
    {
        var lifecycle = StateMachine<SessionStatus, SessionTrigger>.Define(b => b
            .From(SessionStatus.Draft).On(SessionTrigger.Publish).To(SessionStatus.Published)
            .From(SessionStatus.Published).On(SessionTrigger.Cancel).To(SessionStatus.Cancelled)
            .Otherwise(SessionErrors.TransitionNotAllowed));

        var act = () => StateMachineAssert.IsWellFormed(lifecycle, SessionStatus.Draft, s_finals);

        act.Should().Throw<YafAssertionException>()
            .Which.Message.Should().Contain("unreachable")
            .And.Contain($"'{SessionStatus.Completed}'");
    }

    [Fact]
    public void IsWellFormed_StatesThatAreNotAnEnum_ChecksTheStatesOfTheDefinition()
    {
        var lifecycle = StateMachine<string, SessionTrigger>.Define(b => b
            .From("draft").On(SessionTrigger.Publish).To("published")
            .From("archived").On(SessionTrigger.Cancel).To("draft")
            .Otherwise(SessionErrors.TransitionNotAllowed));

        var act = () => StateMachineAssert.IsWellFormed(lifecycle, "draft", ["published"]);

        act.Should().Throw<YafAssertionException>()
            .Which.Message.Should().Contain("'archived'")
            .And.NotContain("'published'");
    }

    private enum SessionStatus
    {
        Draft,
        Published,
        Cancelled,
        Completed,
    }

    private enum SessionTrigger
    {
        Publish,
        Cancel,
        Hold,
    }

    private abstract class SessionErrors : IErrorCatalog
    {
        public static readonly ErrorDescriptor TransitionNotAllowed = Error.Conflict("A session in state {From} cannot take the trigger {Trigger}.");
    }
}

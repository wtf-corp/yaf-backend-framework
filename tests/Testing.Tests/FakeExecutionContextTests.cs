using YAF.Application.Context;
using YAF.Core.Ids;
using YAF.Testing;

namespace Testing.Tests;

public sealed class FakeExecutionContextTests
{
    private static readonly UserId s_user = UserId.From(Guid.Parse("0192f0c1-0000-7000-8000-000000000001"));
    private static readonly TenantId s_tenant = TenantId.From(Guid.Parse("0192f0c1-0000-7000-8000-000000000002"));

    [Fact]
    public void ForUser_ActorIsTheUserInTheTenant()
    {
        var context = FakeExecutionContext.ForUser(s_user, s_tenant);

        context.Actor.Kind.Should().Be(PrincipalKind.User);
        context.Actor.UserId.Should().Be(s_user);
        context.TenantId.Should().Be(s_tenant);
        context.Initiator.Should().BeNull();
        context.Origin.Should().Be("test");
    }

    [Fact]
    public void ForSystem_ActorIsSystemWithTheInitiator()
    {
        var context = FakeExecutionContext.ForSystem("job:send-reminders", s_tenant, s_user);

        context.Actor.Kind.Should().Be(PrincipalKind.System);
        context.Actor.Name.Should().Be("job:send-reminders");
        context.Initiator.Should().Be(new PrincipalRef(PrincipalKind.User, s_user));
        context.TenantId.Should().Be(s_tenant);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ForSystem_WithoutReason_Throws(string reason)
    {
        var act = () => FakeExecutionContext.ForSystem(reason, s_tenant);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Current_ReturnsItself()
    {
        var context = FakeExecutionContext.ForUser(s_user, s_tenant);

        var accessor = context.Should().BeAssignableTo<IExecutionContextAccessor>().Which;

        accessor.Current.Should().BeSameAs(context);
    }

    [Fact]
    public void WithCorrelation_SetsTheCorrelationAndKeepsTheActor()
    {
        var correlation = CorrelationId.From(Guid.Parse("0192f0c1-0000-7000-8000-000000000003"));

        var context = FakeExecutionContext.ForUser(s_user, s_tenant).WithCorrelation(correlation);

        context.CorrelationId.Should().Be(correlation);
        context.Actor.UserId.Should().Be(s_user);
        context.TenantId.Should().Be(s_tenant);
    }
}

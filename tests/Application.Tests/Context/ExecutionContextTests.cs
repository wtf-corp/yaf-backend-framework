using System.Diagnostics;
using YAF.Application.Context;
using YAF.Core.Ids;

namespace Application.Tests.Context;

public sealed class ExecutionContextTests
{
    private static readonly TenantId s_tenant = TenantId.From(Guid.Parse("8f0c2f51-7b0e-4d43-9a43-2d7f6b8f0a01"));
    private static readonly UserId s_user = UserId.From(Guid.Parse("3a3e9c7d-1f6b-4c55-8d0e-6a2b9f4e1c02"));
    private static readonly CorrelationId s_correlation = CorrelationId.From(Guid.Parse("b5d1e2f3-0a4c-4e6b-9c7d-1e2f3a4b5c03"));

    [Fact]
    public void Current_WithoutAScope_ThrowsMissingExecutionContext()
    {
        var accessor = new ExecutionContextScope();

        var act = () => accessor.Current;

        act.Should().Throw<MissingExecutionContextException>().Which.Should().BeAssignableTo<InvalidOperationException>();
    }

    [Fact]
    public void Begin_SetsTheContextForTheScope()
    {
        var accessor = new ExecutionContextScope();
        var initiator = new PrincipalRef(PrincipalKind.User, s_user);
        var seed = new ExecutionContextSeed(s_tenant, Principal.System("job:send-reminders"), initiator, s_correlation, "job");
        using var activity = new Activity("test");
        activity.SetIdFormat(ActivityIdFormat.W3C).Start();

        using (accessor.Begin(seed))
        {
            var context = accessor.Current;

            context.TenantId.Should().Be(s_tenant);
            context.Actor.Should().BeSameAs(seed.Actor);
            context.Initiator.Should().Be(initiator);
            context.CorrelationId.Should().Be(s_correlation);
            context.TraceParent.Should().Be(activity.Id);
            context.Origin.Should().Be("job");
            context.AdditionalTenants.Should().BeEmpty();
            context.IdempotencyKey.Should().BeNull();
            accessor.Current.Should().BeSameAs(context);
        }

        var afterwards = () => accessor.Current;
        afterwards.Should().Throw<MissingExecutionContextException>();
    }

    [Fact]
    public void Begin_Twice_Throws()
    {
        var factory = new ExecutionContextScope();
        var seed = new ExecutionContextSeed(s_tenant, Principal.User(s_user), null, s_correlation, "http");
        factory.Begin(seed).Dispose();

        var act = () => factory.Begin(seed);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Principal_System_RequiresAReason(string? reason)
    {
        var system = () => Principal.System(reason!);
        var service = () => Principal.Service(reason!);

        system.Should().Throw<ArgumentException>();
        service.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Principal_System_KeepsTheReason()
    {
        (Principal.System("cron:purge").Kind, Principal.System("cron:purge").Name).Should().Be((PrincipalKind.System, "cron:purge"));
        (Principal.Service("billing").Kind, Principal.Service("billing").Name).Should().Be((PrincipalKind.Service, "billing"));
        (Principal.Anonymous.Kind, Principal.Anonymous.Name).Should().Be((PrincipalKind.Anonymous, (string?)null));
    }

    [Fact]
    public void Principal_User_KeepsTheUserId()
    {
        var principal = Principal.User(s_user);

        principal.Kind.Should().Be(PrincipalKind.User);
        principal.UserId.Should().Be(s_user);
        principal.Name.Should().BeNull();
    }
}

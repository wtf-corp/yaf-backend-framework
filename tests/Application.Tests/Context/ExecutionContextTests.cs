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

        act.Should().Throw<MissingExecutionContextException>();
    }

    [Fact]
    public void Begin_SetsTheContextForTheScope()
    {
        var accessor = new ExecutionContextScope();
        var initiator = new PrincipalRef(PrincipalKind.User, s_user);
        var seed = new ExecutionContextSeed(s_tenant, Principal.System("job:send-reminders"), initiator, s_correlation, "job");

        using (accessor.Begin(seed))
        {
            var context = accessor.Current;

            context.TenantId.Should().Be(s_tenant);
            context.Actor.Should().BeSameAs(seed.Actor);
            context.Initiator.Should().Be(initiator);
            context.CorrelationId.Should().Be(s_correlation);
            context.Origin.Should().Be("job");
            context.AdditionalTenants.Should().BeEmpty();
            context.IdempotencyKey.Should().BeNull();
            accessor.Current.Should().BeSameAs(context);
        }

        var afterwards = () => accessor.Current;
        afterwards.Should().Throw<MissingExecutionContextException>();
    }

    [Fact]
    public void Begin_TakesTheTraceParentOfTheCurrentActivity()
    {
        var factory = new ExecutionContextScope();
        using var activity = new Activity("test");
        activity.SetIdFormat(ActivityIdFormat.W3C).Start();

        using (factory.Begin(new ExecutionContextSeed(s_tenant, Principal.User(s_user), null, s_correlation, "http")))
        {
            factory.Current.TraceParent.Should().Be(activity.Id);
        }
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

    [Fact]
    public void Begin_WithoutSeed_Throws()
    {
        var factory = new ExecutionContextScope();

        var act = () => factory.Begin(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Principal_System_RequiresAReason(string? reason)
    {
        var act = () => Principal.System(reason!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Principal_System_KeepsTheReason()
    {
        var principal = Principal.System("cron:purge-retention");

        principal.Kind.Should().Be(PrincipalKind.System);
        principal.Name.Should().Be("cron:purge-retention");
        principal.UserId.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Principal_Service_RequiresAName(string? name)
    {
        var act = () => Principal.Service(name!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Principal_Service_KeepsTheName()
    {
        var principal = Principal.Service("registrations");

        principal.Kind.Should().Be(PrincipalKind.Service);
        principal.Name.Should().Be("registrations");
        principal.UserId.Should().BeNull();
    }

    [Fact]
    public void Principal_User_KeepsTheUserId()
    {
        var principal = Principal.User(s_user);

        principal.Kind.Should().Be(PrincipalKind.User);
        principal.UserId.Should().Be(s_user);
        principal.Name.Should().BeNull();
    }

    [Fact]
    public void Principal_Anonymous_HasNoIdentity()
    {
        var principal = Principal.Anonymous;

        principal.Kind.Should().Be(PrincipalKind.Anonymous);
        principal.UserId.Should().BeNull();
        principal.Name.Should().BeNull();
        Principal.Anonymous.Should().BeSameAs(principal);
    }

    [Fact]
    public void MissingExecutionContextException_IsAnInvalidOperation()
    {
        var inner = new InvalidOperationException("inner");

        new MissingExecutionContextException().Message.Should().NotBeNullOrWhiteSpace();
        new MissingExecutionContextException("message").Message.Should().Be("message");
        new MissingExecutionContextException("message", inner).InnerException.Should().BeSameAs(inner);
        new MissingExecutionContextException().Should().BeAssignableTo<InvalidOperationException>();
    }
}

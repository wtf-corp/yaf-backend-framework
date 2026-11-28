using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using YAF.Application.Context;
using YAF.Core.Ids;
using YAF.Persistence.EfCore;
using YAF.Testing;

namespace Persistence.EfCore.Tests.Tenancy;

// Npgsql over a connection string that is never opened, and a last interceptor that suppresses the save: the YAF interceptors run, no
// command reaches a database, and the test reads the tracked entity.
public sealed class TenantInterceptorTests
{
    private const string UnusedConnection = "Host=localhost;Database=unused";

    private readonly TestClock _clock = new();

    [Fact]
    public async Task Add_SetsTheContextsTenant()
    {
        var tenant = TenantId.New();
        using var provider = Build(FakeExecutionContext.ForUser(UserId.New(), tenant));
        using var scope = provider.CreateScope();
        using var context = Context(scope);
        var note = new NoteData { Id = Guid.NewGuid(), Body = "first" };

        context.Add(note);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        note.TenantId.Should().Be(tenant.Value);
    }

    [Fact]
    public async Task Add_OwnTenant_Saves()
    {
        var tenant = TenantId.New();
        using var provider = Build(FakeExecutionContext.ForUser(UserId.New(), tenant));
        using var scope = provider.CreateScope();
        using var context = Context(scope);
        var note = new NoteData { Id = Guid.NewGuid(), Body = "first", TenantId = tenant.Value };

        context.Add(note);
        var saved = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        saved.Should().Be(0);
        note.TenantId.Should().Be(tenant.Value);
    }

    [Fact]
    public async Task Add_ForeignTenant_Throws()
    {
        using var provider = Build(FakeExecutionContext.ForUser(UserId.New(), TenantId.New()));
        using var scope = provider.CreateScope();
        using var context = Context(scope);
        context.Add(new NoteData { Id = Guid.NewGuid(), Body = "first", TenantId = TenantId.New().Value });

        var act = () => context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Update_ForeignTenant_Throws()
    {
        using var provider = Build(FakeExecutionContext.ForUser(UserId.New(), TenantId.New()));
        using var scope = provider.CreateScope();
        using var context = Context(scope);
        var note = new NoteData { Id = Guid.NewGuid(), Body = "first", TenantId = TenantId.New().Value };
        context.Attach(note);

        note.Body = "second";
        var act = () => context.SaveChanges();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Update_TenantMovedFromForeignToOwn_Throws()
    {
        var tenant = TenantId.New();
        using var provider = Build(FakeExecutionContext.ForUser(UserId.New(), tenant));
        using var scope = provider.CreateScope();
        using var context = Context(scope);
        var note = new NoteData { Id = Guid.NewGuid(), Body = "first", TenantId = TenantId.New().Value };
        context.Attach(note);

        note.TenantId = tenant.Value;
        var act = () => context.SaveChanges();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Add_NoTenantInContext_Throws()
    {
        using var provider = Build(new NoTenantContext());
        using var scope = provider.CreateScope();
        using var context = Context(scope);
        context.Add(new NoteData { Id = Guid.NewGuid(), Body = "first" });

        var act = () => context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Message_ContainsNoIds()
    {
        var tenant = TenantId.New();
        var foreign = TenantId.New();
        var id = Guid.NewGuid();
        using var provider = Build(FakeExecutionContext.ForUser(UserId.New(), tenant));
        using var scope = provider.CreateScope();
        using var context = Context(scope);
        context.Add(new NoteData { Id = id, Body = "first", TenantId = foreign.Value });

        var act = () => context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Message.Should().Contain(nameof(NoteData))
            .And.NotContain(tenant.Value.ToString())
            .And.NotContain(foreign.Value.ToString())
            .And.NotContain(id.ToString());
    }

    private static NoteContext Context(IServiceScope scope) => new(
        scope.ServiceProvider.GetRequiredService<DbContextOptions<NoteContext>>(),
        scope.ServiceProvider.GetRequiredService<IYafDbContextServices>());

    private ServiceProvider Build(IExecutionContextAccessor accessor)
    {
        var services = new ServiceCollection();
        services.AddSingleton(accessor);
        services.AddSingleton(_clock.TimeProvider);
        services.AddYafDbContext<NoteContext>(options => options.UseNpgsql(UnusedConnection).AddInterceptors(new SuppressSave()));
        return services.BuildServiceProvider();
    }

    private sealed class NoTenantContext : IExecutionContext, IExecutionContextAccessor
    {
        public TenantId? TenantId => null;

        public Principal Actor { get; } = Principal.System("test:no-tenant");

        public PrincipalRef? Initiator => null;

        public CorrelationId CorrelationId { get; } = CorrelationId.New();

        public string? TraceParent => null;

        public IReadOnlySet<TenantId> AdditionalTenants { get; } = new HashSet<TenantId>();

        public string? IdempotencyKey => null;

        public string Origin => "test";

        public IExecutionContext Current => this;
    }

    private sealed class SuppressSave : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
            => InterceptionResult<int>.SuppressWithResult(0);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
    }

    private sealed class NoteData : AggregateData
    {
        public string Body { get; set; } = string.Empty;
    }

    private sealed class NoteContext(DbContextOptions<NoteContext> options, IYafDbContextServices services) : YafDbContext(options, services)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoteData>().Property(note => note.Body).HasMaxLength(200);
            modelBuilder.ApplyYafModel(this);
        }
    }
}

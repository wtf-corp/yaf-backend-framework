using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using YAF.Application.Context;
using YAF.Core.Ids;
using YAF.Persistence.EfCore;
using YAF.Testing;

namespace Persistence.EfCore.Tests.Interceptors;

// Npgsql over a connection string that is never opened, and a last interceptor that suppresses the save: the YAF interceptors run, no
// command reaches a database, and the test reads the tracked entity.
public sealed class StampingInterceptorTests
{
    private const string UnusedConnection = "Host=localhost;Database=unused";

    private readonly TestClock _clock = new();

    [Fact]
    public async Task Add_StampsCreatedAndModifiedFromClockAndActor()
    {
        var user = UserId.New();
        using var provider = Build(FakeExecutionContext.ForUser(user, TenantId.New()));
        using var scope = provider.CreateScope();
        using var context = Context(scope);
        var note = new NoteData { Id = Guid.NewGuid(), Body = "first" };

        context.Add(note);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        note.CreatedAt.Should().Be(_clock.Now);
        note.ModifiedAt.Should().Be(_clock.Now);
        note.CreatedBy.Should().Be(user.Value);
        note.ModifiedBy.Should().Be(user.Value);
    }

    [Fact]
    public void Update_StampsModifiedOnly()
    {
        var user = UserId.New();
        using var provider = Build(FakeExecutionContext.ForUser(user, TenantId.New()));
        using var scope = provider.CreateScope();
        using var context = Context(scope);
        var createdAt = _clock.Now;
        var createdBy = Guid.NewGuid();
        var note = new NoteData
        {
            Id = Guid.NewGuid(),
            Body = "first",
            CreatedAt = createdAt,
            CreatedBy = createdBy,
            ModifiedAt = createdAt,
            ModifiedBy = createdBy,
        };
        context.Attach(note);
        _clock.Advance(TimeSpan.FromHours(1));

        note.Body = "second";
        context.SaveChanges();

        note.CreatedAt.Should().Be(createdAt);
        note.CreatedBy.Should().Be(createdBy);
        note.ModifiedAt.Should().Be(_clock.Now);
        note.ModifiedBy.Should().Be(user.Value);
    }

    [Fact]
    public async Task Add_WithInitiator_RecordsTheInitiator()
    {
        var initiator = UserId.New();
        using var provider = Build(FakeExecutionContext.ForSystem("job:send-reminders", TenantId.New(), initiator));
        using var scope = provider.CreateScope();
        using var context = Context(scope);
        var note = new NoteData { Id = Guid.NewGuid(), Body = "first" };

        context.Add(note);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        note.CreatedBy.Should().Be(initiator.Value);
        note.ModifiedBy.Should().Be(initiator.Value);
    }

    [Fact]
    public async Task Add_SystemActorWithoutInitiator_RecordsEmptyId()
    {
        using var provider = Build(FakeExecutionContext.ForSystem("cron:purge-retention", TenantId.New()));
        using var scope = provider.CreateScope();
        using var context = Context(scope);
        var note = new NoteData { Id = Guid.NewGuid(), Body = "first", CreatedBy = Guid.NewGuid() };

        context.Add(note);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        note.CreatedBy.Should().Be(Guid.Empty);
        note.ModifiedBy.Should().Be(Guid.Empty);
        note.CreatedAt.Should().Be(_clock.Now);
    }

    [Fact]
    public async Task Save_WithoutContext_Throws()
    {
        using var provider = Build(new NoExecutionContext());
        using var scope = provider.CreateScope();
        using var context = Context(scope);
        context.Add(new NoteData { Id = Guid.NewGuid(), Body = "first" });

        var act = () => context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<MissingExecutionContextException>();
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

    private sealed class NoExecutionContext : IExecutionContextAccessor
    {
        public IExecutionContext Current => throw new MissingExecutionContextException();
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

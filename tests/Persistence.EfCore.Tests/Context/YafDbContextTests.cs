using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using YAF.Application.Context;
using YAF.Core.Ids;
using YAF.Persistence.EfCore;
using YAF.Testing;

namespace Persistence.EfCore.Tests.Context;

// Npgsql over a connection string that is never opened builds the models: no test reaches a database.
public sealed class YafDbContextTests
{
    private const string UnusedConnection = "Host=localhost;Database=unused";

    private readonly TestClock _clock = new();

    private readonly FakeExecutionContext _accessor = FakeExecutionContext.ForUser(UserId.New(), TenantId.New());

    [Fact]
    public void Model_WithoutApplyYafModel_Throws()
    {
        using var provider = Build<WithoutApplyYafModelContext>();
        using var context = new WithoutApplyYafModelContext(Options<WithoutApplyYafModelContext>(provider), Services(provider));

        var act = () => context.Model;

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{nameof(YafModelBuilderExtensions.ApplyYafModel)}*");
    }

    [Fact]
    public void Model_StringWithoutMaxLength_ThrowsNamingIt()
    {
        using var provider = Build<StringWithoutMaxLengthContext>();
        using var context = new StringWithoutMaxLengthContext(Options<StringWithoutMaxLengthContext>(provider), Services(provider));

        var act = () => context.Model;

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{nameof(NoteData)}.{nameof(NoteData.Body)}*");
    }

    [Fact]
    public void AddYafDbContext_CalledTwice_RegistersOnce()
    {
        var services = new ServiceCollection();

        services.AddYafDbContext<ValidContext>(options => options.UseNpgsql(UnusedConnection));
        services.AddYafDbContext<ValidContext>(options => options.UseNpgsql(UnusedConnection));

        services.Count(descriptor => descriptor.ServiceType == typeof(ValidContext)).Should().Be(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IDbContextOptionsConfiguration<ValidContext>)).Should().Be(1);
    }

    [Fact]
    public void AddYafDbContext_AfterPlainAddDbContext_ThrowsNamingTheContext()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ValidContext>(options => options.UseNpgsql(UnusedConnection));

        var act = () => services.AddYafDbContext<ValidContext>(options => options.UseNpgsql(UnusedConnection));

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{nameof(ValidContext)}*");
    }

    [Fact]
    public void Services_ExposeContextAccessorAndTime()
    {
        using var provider = Build<ValidContext>();
        using var context = new ValidContext(Options<ValidContext>(provider), Services(provider));

        var exposed = context.ContextServices;

        exposed.ExecutionContext.Should().BeSameAs(_accessor);
        exposed.TimeProvider.Should().BeSameAs(_clock.TimeProvider);
        context.Entry(new NoteData()).Property(note => note.Body).Metadata.GetMaxLength().Should().Be(200);
    }

    private static DbContextOptions<TContext> Options<TContext>(ServiceProvider provider)
        where TContext : DbContext
        => provider.GetRequiredService<DbContextOptions<TContext>>();

    private static IYafDbContextServices Services(ServiceProvider provider) => provider.GetRequiredService<IYafDbContextServices>();

    private ServiceProvider Build<TContext>()
        where TContext : YafDbContext
    {
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionContextAccessor>(_accessor);
        services.AddSingleton(_clock.TimeProvider);
        services.AddYafDbContext<TContext>(options => options.UseNpgsql(UnusedConnection));
        return services.BuildServiceProvider();
    }

    private sealed class NoteData
    {
        public Guid Id { get; set; }

        public string Body { get; set; } = string.Empty;
    }

    private sealed class WithoutApplyYafModelContext(DbContextOptions<WithoutApplyYafModelContext> options, IYafDbContextServices services)
        : YafDbContext(options, services)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NoteData>().Property(note => note.Body).HasMaxLength(200);
    }

    private sealed class StringWithoutMaxLengthContext(DbContextOptions<StringWithoutMaxLengthContext> options, IYafDbContextServices services)
        : YafDbContext(options, services)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoteData>();
            modelBuilder.ApplyYafModel(this);
        }
    }

    private sealed class ValidContext(DbContextOptions<ValidContext> options, IYafDbContextServices services) : YafDbContext(options, services)
    {
        public IYafDbContextServices ContextServices => Services;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoteData>().Property(note => note.Body).HasMaxLength(200);
            modelBuilder.ApplyYafModel(this);
        }
    }
}

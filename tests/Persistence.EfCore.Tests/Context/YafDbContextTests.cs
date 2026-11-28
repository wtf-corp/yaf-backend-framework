using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using YAF.Application.Context;
using YAF.Persistence.EfCore;
using YAF.Testing;

namespace Persistence.EfCore.Tests.Context;

// The model is built with Npgsql over a connection string that is never opened: no test here reaches a database. Each context is created
// from the options and services AddYafDbContext registers.
public sealed class YafDbContextTests : IDisposable
{
    private const string UnusedConnection = "Host=localhost;Database=unused";

    private readonly TestClock _clock = new();

    private readonly NoExecutionContext _accessor = new();

    private readonly List<IDisposable> _disposables = [];

    [Fact]
    public void Model_WithoutApplyYafModel_Throws()
    {
        var (options, services) = Register<WithoutApplyYafModelContext>();
        using var context = new WithoutApplyYafModelContext(options, services);

        var act = () => context.Model;

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{nameof(YafModelBuilderExtensions.ApplyYafModel)}*");
    }

    [Fact]
    public void Model_StringWithoutMaxLength_ThrowsNamingIt()
    {
        var (options, services) = Register<StringWithoutMaxLengthContext>();
        using var context = new StringWithoutMaxLengthContext(options, services);

        var act = () => context.Model;

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{nameof(NoteData)}.{nameof(NoteData.Body)}*");
    }

    [Fact]
    public void Model_WithApplyYafModelAndMaxLengths_Builds()
    {
        var (options, services) = Register<ValidContext>();
        using var context = new ValidContext(options, services);

        var body = context.Entry(new NoteData()).Property(note => note.Body).Metadata;

        body.GetMaxLength().Should().Be(200);
    }

    [Fact]
    public void AddYafDbContext_CalledTwice_RegistersOnce()
    {
        var services = new ServiceCollection();

        services.AddYafDbContext<ValidContext>(options => options.UseNpgsql(UnusedConnection));
        services.AddYafDbContext<ValidContext>(options => options.UseNpgsql(UnusedConnection));

        services.Count(descriptor => descriptor.ServiceType == typeof(ValidContext)).Should().Be(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IDbContextOptionsConfiguration<ValidContext>)).Should().Be(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IYafDbContextServices)).Should().Be(1);
    }

    [Fact]
    public void Services_ExposeContextAccessorAndTime()
    {
        var (options, services) = Register<ValidContext>();
        using var context = new ValidContext(options, services);

        var exposed = context.ContextServices;

        exposed.ExecutionContext.Should().BeSameAs(_accessor);
        exposed.TimeProvider.Should().BeSameAs(_clock.TimeProvider);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }

    private (DbContextOptions<TContext> Options, IYafDbContextServices Services) Register<TContext>()
        where TContext : YafDbContext
    {
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionContextAccessor>(_accessor);
        services.AddSingleton(_clock.TimeProvider);
        services.AddYafDbContext<TContext>(options => options.UseNpgsql(UnusedConnection));
        var provider = services.BuildServiceProvider(validateScopes: true);
        var scope = provider.CreateScope();
        _disposables.Add(scope);
        _disposables.Add(provider);
        return (scope.ServiceProvider.GetRequiredService<DbContextOptions<TContext>>(),
            scope.ServiceProvider.GetRequiredService<IYafDbContextServices>());
    }

    private sealed class NoExecutionContext : IExecutionContextAccessor
    {
        public IExecutionContext Current => throw new MissingExecutionContextException();
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

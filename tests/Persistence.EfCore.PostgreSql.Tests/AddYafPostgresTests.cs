using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using YAF.Application.Context;
using YAF.Core.Ids;
using YAF.Domain.ValueObjects;
using YAF.Persistence.EfCore;
using YAF.Persistence.EfCore.PostgreSql;
using YAF.Testing;

namespace Persistence.EfCore.PostgreSql.Tests;

// The hosts are built over connection strings that are never opened; the model tests read the generated script.
public sealed class AddYafPostgresTests
{
    private const string Unopened = "Host=localhost;Database=unused";

    [Fact]
    public void Model_UsesSnakeCaseNames()
    {
        var table = Use(Environments.Development, Unopened, context => context.Entry(new SessionRow()).Metadata.GetTableName());
        var script = Use(Environments.Development, Unopened, context => context.Database.GenerateCreateScript());

        table.Should().Be("session_row");
        script.Should().Contain("CREATE TABLE session_row (").And.Contain("max_seats integer NOT NULL,").And.Contain("title character varying(80)");
    }

    [Fact]
    public void ComplexTypeColumns_AreSnakeCase()
    {
        var script = Use(Environments.Development, Unopened, context => context.Database.GenerateCreateScript());

        script.Should().Contain("price_amount numeric NOT NULL,").And.Contain("price_currency character varying(3) NOT NULL,");
    }

    [Theory]
    [InlineData("Price_Amount", "price_amount")]
    [InlineData("ExternalURLPath", "external_url_path")]
    [InlineData("Line2Text", "line2_text")]
    public void SnakeCase_ConvertsTheName(string name, string expected) => SnakeCaseNamingConvention.ToSnakeCase(name).Should().Be(expected);

    [Fact]
    public Task Production_WithoutVerifyFull_FailsAtStartup()
        => FailsAtStartupAsync(Environments.Production, $"{Unopened};SSL Mode=Require", "*ConnectionStrings:catalog*VerifyFull*");

    [Fact]
    public Task MissingConnectionString_FailsAtStartup() => FailsAtStartupAsync(Environments.Development, null, "*ConnectionStrings:catalog*");

    [Fact]
    public Task Production_WithVerifyFull_Starts() => StartsAsync(Environments.Production, $"{Unopened};SSL Mode=VerifyFull");

    [Theory]
    [InlineData(SslMode.Disable)]
    [InlineData(SslMode.Prefer)]
    [InlineData(SslMode.Require)]
    public Task Development_AllowsAnySslMode(SslMode mode) => StartsAsync(Environments.Development, $"{Unopened};SSL Mode={mode}");

    [Fact]
    public void Options_EnableRetryOnFailure()
    {
        var retries = Use(Environments.Development, Unopened, context => context.Database.CreateExecutionStrategy().RetriesOnFailure);

        retries.Should().BeTrue();
    }

    [Fact]
    public void Options_SetACommandTimeoutOf30Seconds()
        => Use(Environments.Development, Unopened, context => context.Database.GetCommandTimeout()).Should().Be(30);

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    public void ErrorDetail_IsOnlyIncludedInDevelopment(string environment, bool expected)
    {
        var connectionString = Use(environment, $"{Unopened};SSL Mode=VerifyFull;Include Error Detail=true", context => context.Database.GetConnectionString());

        new NpgsqlConnectionStringBuilder(connectionString).IncludeErrorDetail.Should().Be(expected);
    }

    private static async Task FailsAtStartupAsync(string environment, string? connectionString, string message)
    {
        using var host = Build(environment, connectionString);

        var act = () => host.StartAsync(TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<OptionsValidationException>().ConfigureAwait(false)).WithMessage(message);
    }

    private static async Task StartsAsync(string environment, string connectionString)
    {
        using var host = Build(environment, connectionString);
        await host.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        await host.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static T Use<T>(string environment, string connectionString, Func<SessionContext, T> read)
    {
        using var host = Build(environment, connectionString);
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var context = new SessionContext(
            services.GetRequiredService<DbContextOptions<SessionContext>>(), services.GetRequiredService<IYafDbContextServices>());
        return read(context);
    }

    private static IHost Build(string environment, string? connectionString)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment });
        builder.Configuration.AddInMemoryCollection([KeyValuePair.Create("ConnectionStrings:catalog", connectionString)]);
        builder.Services.AddSingleton<IExecutionContextAccessor>(FakeExecutionContext.ForUser(UserId.New(), TenantId.New()));
        builder.Services.AddSingleton(new TestClock().TimeProvider);
        builder.AddYafPostgres<SessionContext>("catalog");
        return builder.Build();
    }

    private sealed class SessionRow
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public int MaxSeats { get; set; }

        public Money Price { get; set; } = null!;
    }

    private sealed class SessionContext(DbContextOptions<SessionContext> options, IYafDbContextServices services) : YafDbContext(options, services)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SessionRow>().Property(row => row.Title).HasMaxLength(80);
            modelBuilder.ApplyYafModel(this);
        }
    }
}

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
    [InlineData("TicketData", "ticket_data")]
    [InlineData("Price_Amount", "price_amount")]
    [InlineData("ExternalURLPath", "external_url_path")]
    [InlineData("Line2Text", "line2_text")]
    [InlineData("id", "id")]
    public void SnakeCase_ConvertsTheName(string name, string expected) => SnakeCaseNamingConvention.ToSnakeCase(name).Should().Be(expected);

    [Fact]
    public async Task Production_WithoutVerifyFull_FailsAtStartup()
    {
        using var host = Build(Environments.Production, $"{Unopened};SSL Mode=Require");

        var act = () => host.StartAsync(TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<OptionsValidationException>()).WithMessage("*ConnectionStrings:catalog*VerifyFull*");
    }

    [Fact]
    public async Task Production_WithVerifyFull_Starts()
    {
        using var host = Build(Environments.Production, $"{Unopened};SSL Mode=VerifyFull");

        await host.StartAsync(TestContext.Current.CancellationToken);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(SslMode.Disable)]
    [InlineData(SslMode.Prefer)]
    [InlineData(SslMode.Require)]
    public async Task Development_AllowsAnySslMode(SslMode mode)
    {
        using var host = Build(Environments.Development, $"{Unopened};SSL Mode={mode}");

        await host.StartAsync(TestContext.Current.CancellationToken);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MissingConnectionString_FailsAtStartup()
    {
        using var host = Build(Environments.Development, connectionString: null);

        var act = () => host.StartAsync(TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<OptionsValidationException>()).WithMessage("*ConnectionStrings:catalog*");
    }

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

    [Fact]
    public void AddYafPostgres_Twice_RegistersTheContextOnce()
    {
        var builder = Builder(Environments.Development, Unopened);

        builder.AddYafPostgres<SessionContext>("catalog").AddYafPostgres<SessionContext>("catalog");

        builder.Services.Should().ContainSingle(service => service.ServiceType == typeof(SessionContext));
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
        var builder = Builder(environment, connectionString);
        builder.AddYafPostgres<SessionContext>("catalog");
        return builder.Build();
    }

    private static HostApplicationBuilder Builder(string environment, string? connectionString)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment });
        builder.Configuration.AddInMemoryCollection([KeyValuePair.Create("ConnectionStrings:catalog", connectionString)]);
        builder.Services.AddSingleton<IExecutionContextAccessor>(FakeExecutionContext.ForUser(UserId.New(), TenantId.New()));
        builder.Services.AddSingleton(new TestClock().TimeProvider);
        return builder;
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

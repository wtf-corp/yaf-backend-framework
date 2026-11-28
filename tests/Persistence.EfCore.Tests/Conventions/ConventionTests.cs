using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YAF.Application.Context;
using YAF.Core.Ids;
using YAF.Persistence.EfCore;
using YAF.Persistence.EfCore.Conventions;
using YAF.Testing;

namespace Persistence.EfCore.Tests.Conventions;

// The model is built with Npgsql over a connection string that is never opened; the tests read the generated script.
public sealed class ConventionTests
{
    private const string UnusedConnection = "Host=localhost;Database=unused";

    [Fact]
    public void TypedId_MapsToUuid()
    {
        var (script, converter) = Read(context => context.Entry(new TicketData()).Property(ticket => ticket.Owner).Metadata.GetValueConverter());

        script.Should().Contain("\"Owner\" uuid NOT NULL,");
        converter.Should().BeOfType<TypedIdConverter<TicketId>>();
    }

    [Fact]
    public void NullableTypedId_MapsToNullableUuid()
    {
        var script = CreateScript();

        script.Should().Contain("\"Previous\" uuid,");
    }

    [Fact]
    public void SmartEnumeration_MapsToItsCode()
    {
        var converter = new EnumerationConverter<TicketFormat>();

        var script = CreateScript();

        script.Should().Contain("\"Format\" character varying(64) NOT NULL,");
        converter.ConvertToProvider(TicketFormat.Standard).Should().Be("standard");
        converter.ConvertFromProvider("standard").Should().BeSameAs(TicketFormat.Standard);
    }

    [Fact]
    public void Enum_MapsToString()
    {
        var script = CreateScript();

        script.Should().Contain("\"Status\" character varying(64) NOT NULL,");
    }

    [Fact]
    public void Money_MapsToAmountAndCurrencyColumns()
    {
        var script = CreateScript();

        script.Should().Contain("\"Price_Amount\" numeric NOT NULL,").And.Contain("\"Price_Currency\" character varying(3) NOT NULL,");
    }

    [Fact]
    public void NullablePeriod_MapsToNullableColumns()
    {
        var script = CreateScript();

        script.Should().Contain("\"Schedule_Start\" timestamp with time zone,").And.Contain("\"Schedule_End\" timestamp with time zone,");
    }

    [Fact]
    public void DateRange_MapsToStartAndEndColumns()
    {
        var script = CreateScript();

        script.Should().Contain("\"Validity_Start\" date NOT NULL,").And.Contain("\"Validity_End\" date NOT NULL,");
    }

    [Fact]
    public void EmailAddress_MapsToString()
    {
        var script = CreateScript();

        script.Should().Contain("\"Contact\" character varying(254) NOT NULL,");
    }

    [Fact]
    public void Percentage_MapsToNumeric()
    {
        var script = CreateScript();

        script.Should().Contain("\"Discount\" numeric NOT NULL,");
    }

    [Fact]
    public void TypedIdConverter_RoundTripsTheValue()
    {
        var converter = new TypedIdConverter<TicketId>();
        var id = TicketId.New();

        var stored = converter.ConvertToProvider(id);

        stored.Should().Be(id.Value);
        converter.ConvertFromProvider(stored).Should().Be(id);
    }

    [Fact]
    public void Model_StringInComplexTypeWithoutMaxLength_ThrowsNamingIt()
    {
        using var provider = Build<LabelContext>();
        using var context = new LabelContext(Options<LabelContext>(provider), Services(provider));

        var act = () => context.Add(new LabelledData { Label = new Label { Text = "fixture" } });

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{nameof(Label)}.{nameof(Label.Text)}*");
    }

    private static string CreateScript() => Read(_ => 0).Script;

    private static (string Script, T Value) Read<T>(Func<TicketContext, T> read)
    {
        using var provider = Build<TicketContext>();
        using var context = new TicketContext(Options<TicketContext>(provider), Services(provider));
        return (context.Database.GenerateCreateScript(), read(context));
    }

    private static ServiceProvider Build<TContext>()
        where TContext : YafDbContext
    {
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionContextAccessor>(FakeExecutionContext.ForUser(UserId.New(), TenantId.New()));
        services.AddSingleton(new TestClock().TimeProvider);
        services.AddYafDbContext<TContext>(options => options.UseNpgsql(UnusedConnection));
        return services.BuildServiceProvider();
    }

    private static DbContextOptions<TContext> Options<TContext>(ServiceProvider provider)
        where TContext : DbContext
        => provider.GetRequiredService<DbContextOptions<TContext>>();

    private static IYafDbContextServices Services(ServiceProvider provider) => provider.GetRequiredService<IYafDbContextServices>();

    private sealed class TicketContext(DbContextOptions<TicketContext> options, IYafDbContextServices services) : YafDbContext(options, services)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TicketData>();
            modelBuilder.ApplyYafModel(this);
        }
    }

    private sealed class Label
    {
        public string Text { get; set; } = string.Empty;
    }

    private sealed class LabelledData
    {
        public Guid Id { get; set; }

        public Label Label { get; set; } = new();
    }

    private sealed class LabelContext(DbContextOptions<LabelContext> options, IYafDbContextServices services) : YafDbContext(options, services)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LabelledData>().ComplexProperty(data => data.Label);
            modelBuilder.ApplyYafModel(this);
        }
    }
}

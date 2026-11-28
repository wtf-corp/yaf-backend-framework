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
    [Fact]
    public void TypedId_MapsToUuid()
    {
        var converter = Use(context => context.Entry(new TicketData()).Property(ticket => ticket.Owner).Metadata.GetValueConverter());

        converter.Should().BeOfType<TypedIdConverter<TicketId>>();
        Script().Should().Contain("\"Owner\" uuid NOT NULL,");
    }

    [Fact]
    public void NullableTypedId_MapsToNullableUuid() => Script().Should().Contain("\"Previous\" uuid,");

    [Fact]
    public void SmartEnumeration_MapsToItsCode()
    {
        var converter = new EnumerationConverter<TicketFormat>();

        var stored = converter.ConvertToProvider(TicketFormat.Standard);

        stored.Should().Be("standard");
        converter.ConvertFromProvider(stored).Should().BeSameAs(TicketFormat.Standard);
        Script().Should().Contain("\"Format\" character varying(64) NOT NULL,");
    }

    [Fact]
    public void Enum_MapsToString() => Script().Should().Contain("\"Status\" character varying(64) NOT NULL,");

    [Fact]
    public void Money_MapsToAmountAndCurrencyColumns()
        => Script().Should().Contain("\"Price_Amount\" numeric NOT NULL,").And.Contain("\"Price_Currency\" character varying(3) NOT NULL,");

    [Fact]
    public void NullablePeriod_MapsToNullableColumns()
        => Script().Should().Contain("\"Schedule_Start\" timestamp with time zone,").And.Contain("\"Schedule_End\" timestamp with time zone,");

    [Fact]
    public void DateRange_MapsToStartAndEndColumns()
        => Script().Should().Contain("\"Validity_Start\" date NOT NULL,").And.Contain("\"Validity_End\" date NOT NULL,");

    [Fact]
    public void EmailAddress_MapsToString() => Script().Should().Contain("\"Contact\" character varying(254) NOT NULL,");

    [Fact]
    public void Percentage_MapsToNumeric() => Script().Should().Contain("\"Discount\" numeric NOT NULL,");

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
        using var provider = Provider<LabelContext>();
        using var context = new LabelContext(Options<LabelContext>(provider), provider.GetRequiredService<IYafDbContextServices>());

        var act = () => context.Add(new LabelledData { Label = new Label { Text = "fixture" } });

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{nameof(Label)}.{nameof(Label.Text)}*");
    }

    private static string Script() => Use(context => context.Database.GenerateCreateScript());

    private static T Use<T>(Func<TicketContext, T> read)
    {
        using var provider = Provider<TicketContext>();
        using var context = new TicketContext(Options<TicketContext>(provider), provider.GetRequiredService<IYafDbContextServices>());
        return read(context);
    }

    private static ServiceProvider Provider<TContext>()
        where TContext : YafDbContext
    {
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionContextAccessor>(FakeExecutionContext.ForUser(UserId.New(), TenantId.New()));
        services.AddSingleton(new TestClock().TimeProvider);
        services.AddYafDbContext<TContext>(options => options.UseNpgsql("Host=localhost;Database=unused"));
        return services.BuildServiceProvider();
    }

    private static DbContextOptions<TContext> Options<TContext>(ServiceProvider provider)
        where TContext : DbContext
        => provider.GetRequiredService<DbContextOptions<TContext>>();

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

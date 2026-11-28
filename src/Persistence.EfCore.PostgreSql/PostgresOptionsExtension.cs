using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace YAF.Persistence.EfCore.PostgreSql;

/// <summary>Adds the snake_case naming convention to the convention set of a context registered with <c>AddYafPostgres</c>.</summary>
internal sealed class PostgresOptionsExtension : IDbContextOptionsExtension
{
    public DbContextOptionsExtensionInfo Info => field ??= new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
        => new EntityFrameworkServicesBuilder(services).TryAdd<IConventionSetPlugin, ConventionSetPlugin>();

    public void Validate(IDbContextOptions options)
    {
    }

    private sealed class ConventionSetPlugin : IConventionSetPlugin
    {
        public ConventionSet ModifyConventions(ConventionSet conventionSet)
        {
            conventionSet.Add(new SnakeCaseNamingConvention());
            return conventionSet;
        }
    }

    private sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;

        public override string LogFragment => "using YAF snake_case names ";

        public override int GetServiceProviderHashCode() => 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) => debugInfo["YAF:SnakeCase"] = "1";
    }
}

using System.Reflection;
using System.Text.Json;

namespace Persistence.EfCore.PostgreSql.Tests;

public sealed class PostgreSqlAssemblyTests
{
    [Fact]
    public void References_PersistenceEfCore()
    {
        var dependencies = DependenciesOf("YAF.Persistence.EfCore.PostgreSql");

        dependencies.Should().Contain("YAF.Persistence.EfCore");
    }

    [Fact]
    public void References_NpgsqlProvider()
    {
        var dependencies = DependenciesOf("YAF.Persistence.EfCore.PostgreSql");

        dependencies.Should().Contain("Npgsql.EntityFrameworkCore.PostgreSQL");
    }

    [Fact]
    public void Metadata_AotAndTrimming_AreNotDeclared()
    {
        var assembly = Assembly.Load("YAF.Persistence.EfCore.PostgreSql");

        var keys = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Select(attribute => attribute.Key);

        keys.Should().NotContain(["IsTrimmable", "IsAotCompatible"]);
    }

    // YAF.Persistence.EfCore.PostgreSql has no type yet, so the compiler emits no assembly reference; the dependency graph is read from
    // the deps file, which lists the project and package references as the package's dependencies list them (01 §3).
    private static List<string> DependenciesOf(string library)
    {
        var depsFile = Path.Combine(AppContext.BaseDirectory, $"{typeof(PostgreSqlAssemblyTests).Assembly.GetName().Name}.deps.json");
        using var deps = JsonDocument.Parse(File.ReadAllText(depsFile));

        return deps.RootElement.GetProperty("targets").EnumerateObject().Single().Value.EnumerateObject()
            .Single(entry => entry.Name.StartsWith($"{library}/", StringComparison.Ordinal)).Value
            .GetProperty("dependencies").EnumerateObject().Select(dependency => dependency.Name).ToList();
    }
}

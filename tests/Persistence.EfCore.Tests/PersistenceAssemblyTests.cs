using System.Reflection;
using System.Text.Json;

namespace Persistence.EfCore.Tests;

public sealed class PersistenceAssemblyTests
{
    [Fact]
    public void References_Infrastructure()
    {
        var dependencies = DependenciesOf("YAF.Persistence.EfCore");

        dependencies.Should().Contain("YAF.Infrastructure");
    }

    [Fact]
    public void References_EntityFrameworkCoreRelational()
    {
        var dependencies = DependenciesOf("YAF.Persistence.EfCore");

        dependencies.Should().Contain("Microsoft.EntityFrameworkCore.Relational");
    }

    [Fact]
    public void Metadata_AotAndTrimming_AreNotDeclared()
    {
        var assembly = Assembly.Load("YAF.Persistence.EfCore");

        var keys = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Select(attribute => attribute.Key);

        keys.Should().NotContain(["IsTrimmable", "IsAotCompatible"]);
    }

    // YAF.Persistence.EfCore has no type yet, so the compiler emits no assembly reference; the dependency graph is read from the deps
    // file, which lists the project and package references as the package's dependencies list them (01 §3).
    private static List<string> DependenciesOf(string library)
    {
        var depsFile = Path.Combine(AppContext.BaseDirectory, $"{typeof(PersistenceAssemblyTests).Assembly.GetName().Name}.deps.json");
        using var deps = JsonDocument.Parse(File.ReadAllText(depsFile));

        return deps.RootElement.GetProperty("targets").EnumerateObject().Single().Value.EnumerateObject()
            .Single(entry => entry.Name.StartsWith($"{library}/", StringComparison.Ordinal)).Value
            .GetProperty("dependencies").EnumerateObject().Select(dependency => dependency.Name).ToList();
    }
}

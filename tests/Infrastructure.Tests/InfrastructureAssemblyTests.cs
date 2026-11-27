using System.Reflection;
using System.Text.Json;

namespace Infrastructure.Tests;

public sealed class InfrastructureAssemblyTests
{
    [Fact]
    public void References_Application()
    {
        // YAF.Infrastructure has no type yet, so the compiler emits no assembly reference; the dependency graph is read from the
        // deps file, which lists the project references as the package's dependencies list them (01 §3).
        var depsFile = Path.Combine(AppContext.BaseDirectory, $"{typeof(InfrastructureAssemblyTests).Assembly.GetName().Name}.deps.json");
        using var deps = JsonDocument.Parse(File.ReadAllText(depsFile));

        var dependencies = deps.RootElement.GetProperty("targets").EnumerateObject().Single().Value.EnumerateObject()
            .Single(library => library.Name.StartsWith("YAF.Infrastructure/", StringComparison.Ordinal)).Value
            .GetProperty("dependencies").EnumerateObject().Select(dependency => dependency.Name);

        dependencies.Should().Contain("YAF.Application");
    }

    [Fact]
    public void Metadata_AotAndTrimming_AreNotDeclared()
    {
        var assembly = Assembly.Load("YAF.Infrastructure");

        var keys = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Select(attribute => attribute.Key);

        keys.Should().NotContain(["IsTrimmable", "IsAotCompatible"]);
    }
}

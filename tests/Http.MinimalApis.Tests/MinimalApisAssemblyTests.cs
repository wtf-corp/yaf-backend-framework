using System.Reflection;
using System.Text.Json;

namespace Http.MinimalApis.Tests;

public sealed class MinimalApisAssemblyTests
{
    [Fact]
    public void References_Http()
    {
        var dependencies = DirectDependenciesOf("YAF.Http.MinimalApis");

        dependencies.Should().Contain("YAF.Http");
    }

    [Fact]
    public void References_NoSanitizerAndNoVersioning()
    {
        var dependencies = AllDependenciesOf("YAF.Http.MinimalApis");

        dependencies.Should().NotContain(name => name.Contains("Sanitizer", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Asp.Versioning", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Metadata_AotAndTrimming_AreNotDeclared()
    {
        var assembly = Assembly.Load("YAF.Http.MinimalApis");

        var keys = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Select(attribute => attribute.Key);

        keys.Should().NotContain(["IsTrimmable", "IsAotCompatible"]);
    }

    // YAF.Http.MinimalApis has no type yet, so the compiler emits no assembly reference; the dependency graph is read from the deps file,
    // which lists the project and package references as the package's dependencies list them (01 §3, ADR-0003).
    private static List<string> DirectDependenciesOf(string library) => [.. Libraries()[library]];

    private static HashSet<string> AllDependenciesOf(string library)
    {
        var libraries = Libraries();
        var found = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(libraries[library]);

        while (pending.TryPop(out var name))
        {
            if (found.Add(name) && libraries.TryGetValue(name, out var next))
            {
                foreach (var dependency in next)
                {
                    pending.Push(dependency);
                }
            }
        }

        return found;
    }

    private static Dictionary<string, List<string>> Libraries()
    {
        var depsFile = Path.Combine(AppContext.BaseDirectory, $"{typeof(MinimalApisAssemblyTests).Assembly.GetName().Name}.deps.json");
        using var deps = JsonDocument.Parse(File.ReadAllText(depsFile));

        return deps.RootElement.GetProperty("targets").EnumerateObject().Single().Value.EnumerateObject().ToDictionary(
            entry => entry.Name[..entry.Name.IndexOf('/', StringComparison.Ordinal)],
            entry => entry.Value.TryGetProperty("dependencies", out var dependencies)
                ? dependencies.EnumerateObject().Select(dependency => dependency.Name).ToList()
                : [],
            StringComparer.Ordinal);
    }
}

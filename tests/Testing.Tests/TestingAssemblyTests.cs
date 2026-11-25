using System.Reflection;

namespace Testing.Tests;

public sealed class TestingAssemblyTests
{
    // 15 §3: the helpers throw their own YafAssertionException, so YAF.Testing references no test framework or assertion library.
    private static readonly string[] s_forbiddenPrefixes =
    [
        "xunit", "NUnit", "MSTest", "Microsoft.VisualStudio.TestPlatform", "TUnit",
        "AwesomeAssertions", "FluentAssertions", "Shouldly",
    ];

    [Fact]
    public void References_NoTestFrameworkOrAssertionLibrary()
    {
        var assembly = Assembly.Load("YAF.Testing");

        var forbidden = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => s_forbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

        forbidden.Should().BeEmpty();
    }
}

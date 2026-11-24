namespace Analyzers.Tests;

public sealed class GeneratorHarnessTests
{
    [Fact]
    public async Task EmptySnippet_CompilesWithoutDiagnostics()
    {
        const string source = "namespace Probe; public static class Empty { }";

        using var result = await GeneratorHarness.RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
        result.Assembly!.GetType("Probe.Empty").Should().NotBeNull();
    }
}

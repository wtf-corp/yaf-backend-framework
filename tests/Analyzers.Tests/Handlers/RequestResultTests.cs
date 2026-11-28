using System.Globalization;
using Microsoft.CodeAnalysis;
using YAF.Analyzers.Handlers;

namespace Analyzers.Tests.Handlers;

public sealed class RequestResultTests
{
    private const string Usings = """
        using YAF.Application;
        using YAF.Core.Results;

        """;

    [Fact]
    public async Task RequestOfString_ReportsYAF4001()
    {
        const string source = Usings + """
            namespace Probe;
            public sealed record Ping : IQuery<string>;
            """;

        using var result = await RunAsync(source);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("YAF4001");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should()
            .Be("The request 'Probe.Ping' has the result type 'string', which is neither Result nor Result<T>");
        Text(diagnostic).Should().Be("Ping");
    }

    [Fact]
    public async Task StreamQueryIsNotChecked_ReportsNothing()
    {
        const string source = Usings + """
            namespace Probe;
            public sealed record Search : IStreamQuery<string>;
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestOfResultOfT_ReportsNothing()
    {
        const string source = Usings + """
            namespace Probe;
            public sealed record Ping : IQuery<Result<string>>;
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestOfResult_ReportsNothing()
    {
        const string source = Usings + """
            namespace Probe;
            public sealed record Close : ICommand<Result>;
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task InterfaceThatPassesTheResultOn_ReportsNothing()
    {
        const string source = Usings + """
            namespace Probe;
            public interface IAuditedQuery<TResult> : IQuery<TResult>;
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
    }

    private static Task<HarnessResult> RunAsync(string source) =>
        GeneratorHarness.RunAsync(source, analyzers: [new RequestResultAnalyzer()]);

    private static string Text(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.ToString().Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length);
}

using System.Globalization;
using Microsoft.CodeAnalysis;
using YAF.Analyzers.ErrorCodes;

namespace Analyzers.Tests.ErrorCodes;

public sealed class TemplateArgumentTests
{
    private static readonly Dictionary<string, string> s_catalog = new() { ["YafServiceCode"] = "catalog" };

    [Fact]
    public async Task WithoutCapacityArgument_ReportsYAF3003()
    {
        var source = Snippet("""Errors.Full.With(("Title", title))""");

        using var result = await RunAsync(source);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("YAF3003");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should()
            .Be("The placeholder '{Capacity}' of the message template of 'Errors.Full' has no argument");
        Text(diagnostic).Should().Be("""Errors.Full.With(("Title", title))""");
    }

    [Fact]
    public async Task AllPlaceholdersSupplied_ReportsNothing()
    {
        var source = Snippet("""Errors.Full.With(("Capacity", 3), ("Title", title))""");

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task NonConstantName_ReportsNothing()
    {
        var source = Snippet("""Errors.Full.With((title, 3), ("Title", title))""");

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task NonConstantTemplate_ReportsNothing()
    {
        var source = Snippet("""Errors.Computed.With(("Title", title))""");

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
    }

    private static string Snippet(string call) => $$"""
        using YAF.Core.Results;
        namespace Probe;
        public sealed class Errors : IErrorCatalog
        {
            private const string Seats = " (capacity {Capacity})";
            private static readonly string s_template = "Session {Title} is full.";
            public static readonly ErrorDescriptor Full = Error.Conflict("Session {Title} has no free seats" + Seats + ".");
            public static readonly ErrorDescriptor Computed = Error.Conflict(s_template + " {Capacity}");
        }
        public static class Handler
        {
            public static Error Handle(string title) => {{call}};
        }
        """;

    private static Task<HarnessResult> RunAsync(string source) =>
        GeneratorHarness.RunAsync(source, [new ErrorCodeGenerator()], [new TemplateArgumentAnalyzer()], s_catalog);

    private static string Text(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.ToString().Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length);
}

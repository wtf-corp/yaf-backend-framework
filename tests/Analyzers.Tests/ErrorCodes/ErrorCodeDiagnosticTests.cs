using Microsoft.CodeAnalysis;
using YAF.Analyzers.ErrorCodes;

namespace Analyzers.Tests.ErrorCodes;

public sealed class ErrorCodeDiagnosticTests
{
    private static readonly Dictionary<string, string> s_catalog = new() { ["YafServiceCode"] = "catalog" };

    [Fact]
    public async Task MissingServiceCode_ReportsYAF3004()
    {
        var source = Catalogue("public static readonly ErrorDescriptor Full = Error.Conflict(\"Session is full.\");");

        using var result = await RunAsync(source, buildProperties: null);

        ShouldReportYaf3004At(result, "Error.Conflict(\"Session is full.\")");
    }

    [Theory]
    [InlineData("Catalog")]
    [InlineData("cat.alog")]
    [InlineData("catalög")]
    public async Task InvalidServiceCode_ReportsYAF3004(string serviceCode)
    {
        var source = Catalogue("public static readonly ErrorDescriptor Full = Error.Conflict(\"Session is full.\");");

        using var result = await RunAsync(source, new Dictionary<string, string> { ["YafServiceCode"] = serviceCode });

        ShouldReportYaf3004At(result, "Error.Conflict(\"Session is full.\")");
    }

    [Fact]
    public async Task UpperCaseErrorCode_ReportsYAF3004()
    {
        var source = Catalogue("[ErrorCode(\"catalog.session.Title_required\")] public static readonly ErrorDescriptor TitleRequired = Error.Validation(\"Title.\");");

        using var result = await RunAsync(source, s_catalog);

        ShouldReportYaf3004At(result, "Error.Validation(\"Title.\")");
    }

    [Fact]
    public async Task CodeOver100Characters_ReportsYAF3004()
    {
        var code = "catalog." + new string('x', 93);
        var source = Catalogue($"[ErrorCode(\"{code}\")] public static readonly ErrorDescriptor TitleRequired = Error.Validation(\"Title.\");");

        using var result = await RunAsync(source, s_catalog);

        ShouldReportYaf3004At(result, "Error.Validation(\"Title.\")");
    }

    [Fact]
    public async Task CodeOf100Characters_ReportsNothing()
    {
        var code = "catalog." + new string('x', 92);
        var source = Catalogue($"[ErrorCode(\"{code}\")] public static readonly ErrorDescriptor TitleRequired = Error.Validation(\"Title.\");");

        using var result = await RunAsync(source, s_catalog);

        result.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData(".catalog.session.title")]
    [InlineData("catalog.session.title.")]
    [InlineData("catalog.session-title")]
    [InlineData("")]
    public async Task ExplicitCodeBreakingRules_ReportsYAF3004(string code)
    {
        var source = Catalogue($"[ErrorCode(\"{code}\")] public static readonly ErrorDescriptor TitleRequired = Error.Validation(\"Title.\");");

        using var result = await RunAsync(source, s_catalog);

        ShouldReportYaf3004At(result, "Error.Validation(\"Title.\")");
    }

    [Fact]
    public async Task NonAsciiField_ComputedCode_ReportsYAF3004()
    {
        var source = Catalogue("public static readonly ErrorDescriptor Größe = Error.Validation(\"Size.\");");

        using var result = await RunAsync(source, s_catalog);

        ShouldReportYaf3004At(result, "Error.Validation(\"Size.\")");
    }

    [Fact]
    public async Task ComputedCodeOver100Characters_ReportsYAF3004()
    {
        var field = new string('X', 100);
        var source = Catalogue($"public static readonly ErrorDescriptor {field} = Error.Validation(\"Long.\");");

        using var result = await RunAsync(source, s_catalog);

        ShouldReportYaf3004At(result, "Error.Validation(\"Long.\")");
    }

    [Fact]
    public async Task ExplicitCodeWithoutServiceCode_ReportsNothing()
    {
        var source = Catalogue("[ErrorCode(\"catalog.session.title_required\")] public static readonly ErrorDescriptor TitleRequired = Error.Validation(\"Title.\");");

        using var result = await RunAsync(source, buildProperties: null);

        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidCatalogue_ReportsNothing()
    {
        var source = Catalogue("""
            public static readonly ErrorDescriptor Full = Error.Conflict("Session is full.");
                    public static readonly ErrorDescriptor HTTPRequest2Failed = Error.Unavailable("The request failed.");
                    [ErrorCode("catalog.session.title_required_2")]
                    public static readonly ErrorDescriptor TitleRequired = Error.Validation("Title is required.");
            """);

        using var result = await RunAsync(source, new Dictionary<string, string> { ["YafServiceCode"] = "catalog_2" });

        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ConstructorFieldUpperCaseCode_ReportsYAF3004()
    {
        var source = Catalogue(
            "public static readonly ErrorDescriptor Full = new ErrorDescriptor(\"Catalog.Session.Full\", ErrorKind.Conflict, \"Session is full.\");");

        using var result = await RunAsync(source, s_catalog);

        ShouldReportYaf3004At(result, "new ErrorDescriptor(\"Catalog.Session.Full\", ErrorKind.Conflict, \"Session is full.\")");
    }

    [Fact]
    public async Task TargetTypedConstructorFieldCodeOver100Characters_ReportsYAF3004()
    {
        var code = "catalog." + new string('x', 93);
        var source = Catalogue($"public static readonly ErrorDescriptor TitleRequired = new(\"{code}\", ErrorKind.Validation, \"Title.\");");

        using var result = await RunAsync(source, s_catalog);

        ShouldReportYaf3004At(result, $"new(\"{code}\", ErrorKind.Validation, \"Title.\")");
    }

    [Fact]
    public async Task ConstructorFieldNamedArgumentsOutOfOrder_ReportsYAF3004()
    {
        // The template is a valid code, so reading the first argument as the code would report nothing.
        var creation = "new ErrorDescriptor(MessageTemplate: \"catalog.session.full\", Kind: ErrorKind.Conflict, Code: \"Catalog.Session.Full\")";
        var source = Catalogue($"public static readonly ErrorDescriptor Full = {creation};");

        using var result = await RunAsync(source, s_catalog);

        ShouldReportYaf3004At(result, creation);
    }

    [Fact]
    public async Task ConstructorFieldWithoutServiceCode_ReportsNothing()
    {
        var source = Catalogue("""
            public static readonly ErrorDescriptor Full = new ErrorDescriptor("catalog.session.full", ErrorKind.Conflict, "Session is full.");
                    public static readonly ErrorDescriptor TitleRequired = new("catalog.session.title_required", ErrorKind.Validation, "Title.");
            """);

        using var result = await RunAsync(source, buildProperties: null);

        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ConstructorFieldNonConstantCode_ReportsNothing()
    {
        var source = Catalogue("""
            private static readonly string s_code = "Catalog.Session.Full";
                    public static readonly ErrorDescriptor Full = new(s_code, ErrorKind.Conflict, "Session is full.");
            """);

        using var result = await RunAsync(source, s_catalog);

        result.Diagnostics.Should().BeEmpty();
    }

    private static string Catalogue(string members) => $$"""
        using YAF.Core.Results;
        namespace Probe;
        public static class Session
        {
            public sealed class Errors : IErrorCatalog
            {
                {{members}}
            }
        }
        """;

    private static Task<HarnessResult> RunAsync(string source, IReadOnlyDictionary<string, string>? buildProperties) =>
        GeneratorHarness.RunAsync(source, [new ErrorCodeGenerator()], [new ErrorCodeAnalyzer()], buildProperties);

    private static void ShouldReportYaf3004At(HarnessResult result, string call)
    {
        var diagnostic = result.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("YAF3004");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.Location.SourceTree!.ToString().Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length)
            .Should().Be(call);
    }
}

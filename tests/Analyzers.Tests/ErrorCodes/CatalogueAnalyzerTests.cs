using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using YAF.Analyzers.ErrorCodes;

namespace Analyzers.Tests.ErrorCodes;

public sealed class CatalogueAnalyzerTests
{
    private static readonly Dictionary<string, string> s_catalog = new() { ["YafServiceCode"] = "catalog" };

    private static readonly string[] s_coreFiles = ["CoreErrors.cs", "Error.cs", "ErrorDescriptor.cs", "ErrorKind.cs", "IErrorCatalog.cs"];

    [Fact]
    public async Task DescriptorCreatedInMethod_ReportsYAF3001()
    {
        var source = Snippet("""
            public sealed class Errors : IErrorCatalog
            {
                public static ErrorDescriptor Make() => new ErrorDescriptor("catalog.session.full", ErrorKind.Conflict, "Full.");
            }
            """);

        using var result = await RunAsync(source);

        ShouldReportYaf3001At(result, "new ErrorDescriptor(\"catalog.session.full\", ErrorKind.Conflict, \"Full.\")");
    }

    [Fact]
    public async Task NewErrorInHandler_ReportsYAF3001()
    {
        var source = Snippet("""
            public sealed class Handler
            {
                public Result Handle() => new Error("catalog.session.full", ErrorKind.Conflict, "Full.");
            }
            """);

        using var result = await RunAsync(source);

        ShouldReportYaf3001At(result, "new Error(\"catalog.session.full\", ErrorKind.Conflict, \"Full.\")");
    }

    [Fact]
    public async Task FactoryCallInLocal_ReportsYAF3001()
    {
        var source = Snippet("""
            public sealed class Errors : IErrorCatalog
            {
                public static ErrorDescriptor Make()
                {
                    var full = Error.Conflict("Full.");
                    return full;
                }
            }
            """);

        using var result = await RunAsync(source);

        ShouldReportYaf3001At(result, "Error.Conflict(\"Full.\")");
    }

    [Theory]
    [InlineData("Plain", "public static readonly ErrorDescriptor Full = new(\"catalog.full\", ErrorKind.Conflict, \"Full.\");", "new(\"catalog.full\", ErrorKind.Conflict, \"Full.\")")]
    [InlineData("Errors : IErrorCatalog", "public readonly ErrorDescriptor Full = new(\"catalog.full\", ErrorKind.Conflict, \"Full.\");", "new(\"catalog.full\", ErrorKind.Conflict, \"Full.\")")]
    [InlineData("Errors : IErrorCatalog", "public static ErrorDescriptor Full { get; } = Error.Conflict(\"Full.\");", "Error.Conflict(\"Full.\")")]
    [InlineData("Errors : IErrorCatalog", "public static readonly Error Full = Error.Conflict(\"Full.\").ToError();", "Error.Conflict(\"Full.\")")]
    public async Task OutsideCatalogueStaticField_ReportsYAF3001(string type, string member, string creation)
    {
        var source = Snippet($$"""
            public sealed class {{type}}
            {
                {{member}}
            }
            """);

        using var result = await RunAsync(source);

        ShouldReportYaf3001At(result, creation);
    }

    [Fact]
    public async Task CatalogueStaticField_ReportsNothing()
    {
        var source = Snippet("""
            public static class Session
            {
                public sealed class Errors : IErrorCatalog
                {
                    public static readonly ErrorDescriptor Full = Error.Conflict("Session is full.");
                    public static readonly ErrorDescriptor Closed = new("catalog.session.closed", ErrorKind.Conflict, "Closed.");
                    public static readonly Error Gone = new Error("catalog.session.gone", ErrorKind.NotFound, "Gone.");
                }
            }
            """);

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task CoreErrors_ReportsNothing()
    {
        // The sources of YAF.Core that create errors: CoreErrors (a catalogue field) and ToError, With and At of ErrorDescriptor.
        var token = TestContext.Current.CancellationToken;
        var folder = Path.Combine(RepositoryRoot(), "src", "Core", "Results");
        var trees = s_coreFiles
            .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(folder, file)), path: file, cancellationToken: token))
            .Append(CSharpSyntaxTree.ParseText("global using System; global using System.Collections.Generic;", cancellationToken: token));
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(path => !string.Equals(Path.GetFileName(path), "YAF.Core.dll", StringComparison.OrdinalIgnoreCase));
        var compilation = CSharpCompilation.Create(
            "CoreResults",
            trees,
            platform.Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var diagnostics = await compilation.WithAnalyzers([new CatalogueAnalyzer()]).GetAllDiagnosticsAsync(token);

        diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning).Should().BeEmpty();
    }

    private static string Snippet(string types) => $$"""
        using YAF.Core.Results;
        namespace Probe;
        {{types}}
        """;

    private static Task<HarnessResult> RunAsync(string source) =>
        GeneratorHarness.RunAsync(source, [new ErrorCodeGenerator()], [new ErrorCodeAnalyzer(), new CatalogueAnalyzer()], s_catalog);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, "YAF.slnx")))
        {
            directory = directory.Parent ?? throw new InvalidOperationException("YAF.slnx was not found above the test output.");
        }

        return directory.FullName;
    }

    private static void ShouldReportYaf3001At(HarnessResult result, string creation)
    {
        var diagnostic = result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "YAF3001").Which;
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.Location.SourceTree!.ToString().Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length)
            .Should().Be(creation);
    }
}

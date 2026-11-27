using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Analyzers.Tests;

/// <summary>
/// Compiles a snippet against YAF.Core in memory, runs generators and analyzers on it, emits it and loads the result
/// (specs/0006-core/research.md §7).
/// </summary>
internal static class GeneratorHarness
{
    // The namespace of the generated interceptors, as YAF.Core's props file allows it for consumers (plan 0006, T16).
    private static readonly CSharpParseOptions s_parseOptions = new CSharpParseOptions(LanguageVersion.Latest)
        .WithFeatures([new("InterceptorsNamespaces", "YAF.Generated")]);

    public static async Task<HarnessResult> RunAsync(
        string source,
        IEnumerable<IIncrementalGenerator>? generators = null,
        IEnumerable<DiagnosticAnalyzer>? analyzers = null,
        IReadOnlyDictionary<string, string>? buildProperties = null,
        string assemblyName = "Snippet",
        IEnumerable<MetadataReference>? references = null)
    {
        var options = new BuildPropertyOptionsProvider(buildProperties ?? new Dictionary<string, string>());
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, s_parseOptions)],
            References().Concat(references ?? []),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(
            (generators ?? []).Select(generator => generator.AsSourceGenerator()),
            parseOptions: s_parseOptions,
            optionsProvider: options);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var generated, out var generatorDiagnostics);

        var analyzerList = (analyzers ?? []).ToImmutableArray();
        var compilationDiagnostics = analyzerList.IsEmpty
            ? generated.GetDiagnostics()
            : await generated.WithAnalyzers(analyzerList, new AnalyzerOptions([], options)).GetAllDiagnosticsAsync().ConfigureAwait(false);

        var diagnostics = generatorDiagnostics.AddRange(compilationDiagnostics)
            .Where(diagnostic => diagnostic.Severity != DiagnosticSeverity.Hidden)
            .ToImmutableArray();

        using var stream = new MemoryStream();
        if (!generated.Emit(stream).Success)
        {
            return new HarnessResult(diagnostics, null, null, null);
        }

        stream.Position = 0;
        var context = new AssemblyLoadContext(assemblyName, isCollectible: true);
        return new HarnessResult(diagnostics, stream.ToArray(), context, context.LoadFromStream(stream));
    }

    private static IEnumerable<MetadataReference> References()
    {
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        return platform
            .Append(Assembly.Load("YAF.Core").Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
    }

    private sealed class BuildPropertyOptionsProvider(IReadOnlyDictionary<string, string> properties) : AnalyzerConfigOptionsProvider
    {
        private readonly BuildPropertyOptions _global = new(properties);

        public override AnalyzerConfigOptions GlobalOptions => _global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => BuildPropertyOptions.Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => BuildPropertyOptions.Empty;
    }

    private sealed class BuildPropertyOptions(IReadOnlyDictionary<string, string> properties) : AnalyzerConfigOptions
    {
        public static readonly BuildPropertyOptions Empty = new(new Dictionary<string, string>());

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
        {
            const string prefix = "build_property.";
            value = null;
            return key.StartsWith(prefix, StringComparison.Ordinal) && properties.TryGetValue(key[prefix.Length..], out value);
        }
    }
}

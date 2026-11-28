using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace YAF.Analyzers.Handlers;

/// <summary>
/// Reports <c>YAF4005</c> on each class or struct that implements <c>IRequestHandler&lt;TRequest, TResult&gt;</c> or
/// <c>IStreamQueryHandler&lt;TQuery, TItem&gt;</c> for a request or stream query that another handler of the same compilation also
/// handles, naming the others, so that the dispatcher has exactly one handler per request type (04 §5 item 1). An abstract type, which the
/// handler registry does not register, is not counted. A duplicate across assemblies, which no compilation sees, is refused at startup.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class DuplicateHandlerAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor DuplicateHandler = new(
        "YAF4005",
        "Two handlers for one request",
        "The request '{0}' is handled by '{1}' and also by '{2}'",
        "YAF.Application",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The dispatcher runs exactly one handler per request type. Remove the other handlers, or give each its own request.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [DuplicateHandler];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var contracts = new[]
                {
                    start.Compilation.GetTypeByMetadataName("YAF.Application.IRequestHandler`2"),
                    start.Compilation.GetTypeByMetadataName("YAF.Application.IStreamQueryHandler`2"),
                }
                .OfType<INamedTypeSymbol>()
                .ToImmutableArray();
            if (contracts.IsEmpty)
            {
                return;
            }

            var handlers = new ConcurrentBag<Handled>();
            start.RegisterSymbolAction(symbol => Collect(symbol, contracts, handlers), SymbolKind.NamedType);
            start.RegisterCompilationEndAction(end => Report(end, handlers));
        });
    }

    private static void Collect(SymbolAnalysisContext context, ImmutableArray<INamedTypeSymbol> contracts, ConcurrentBag<Handled> handlers)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.IsAbstract || type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return;
        }

        foreach (var contract in type.AllInterfaces)
        {
            if (contracts.Contains(contract.OriginalDefinition, SymbolEqualityComparer.Default)
                && contract.TypeArguments[0] is { TypeKind: not TypeKind.Error } request)
            {
                handlers.Add(new Handled(request, type));
            }
        }
    }

    private static void Report(CompilationAnalysisContext context, ConcurrentBag<Handled> handlers)
    {
        foreach (var group in handlers.GroupBy<Handled, ITypeSymbol>(static entry => entry.Request, SymbolEqualityComparer.Default))
        {
            // One type that handles a request through both contracts is still one handler.
            var types = group.Select(static entry => entry.Handler).Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();
            if (types.Count < 2)
            {
                continue;
            }

            foreach (var handler in types)
            {
                var others = types
                    .Where(other => !SymbolEqualityComparer.Default.Equals(other, handler))
                    .Select(static other => other.ToDisplayString())
                    .OrderBy(static name => name, StringComparer.Ordinal);
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateHandler, handler.Locations[0], group.Key.ToDisplayString(), handler.ToDisplayString(), string.Join("', '", others)));
            }
        }
    }

    private sealed record Handled(ITypeSymbol Request, INamedTypeSymbol Handler);
}

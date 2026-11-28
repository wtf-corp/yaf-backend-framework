using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace YAF.Analyzers.Handlers;

/// <summary>
/// Reports <c>YAF4001</c> on a class or struct that implements <c>IRequest&lt;TResult&gt;</c>, directly or through <c>ICommand</c> or
/// <c>IQuery</c>, with a <c>TResult</c> that is neither <c>Result</c> nor <c>Result&lt;T&gt;</c>, so that middleware can always inspect
/// the outcome (04 §2). Only in a compilation that has YAF.Application's <c>IRequest&lt;TResult&gt;</c>. An interface, which passes its
/// result type on to the types that implement it, a stream query, which is no request, and generated code are not reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class RequestResultAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor NotAResult = new(
        "YAF4001",
        "Request result type is not a result",
        "The request '{0}' has the result type '{1}', which is neither Result nor Result<T>",
        "YAF.Application",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Middleware inspects the outcome of every request. Declare the request as ICommand or IQuery of Result or Result<T>, "
            + "and return a failure as a result with an error of a catalogue.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [NotAResult];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var request = start.Compilation.GetTypeByMetadataName("YAF.Application.IRequest`1");
            var result = start.Compilation.GetTypeByMetadataName("YAF.Core.Results.Result");
            var resultOfT = start.Compilation.GetTypeByMetadataName("YAF.Core.Results.Result`1");
            if (request is null || result is null || resultOfT is null)
            {
                return;
            }

            start.RegisterSymbolAction(symbol => Analyze(symbol, request, result, resultOfT), SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol request, INamedTypeSymbol result, INamedTypeSymbol resultOfT)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return;
        }

        foreach (var contract in type.AllInterfaces)
        {
            var argument = contract.TypeArguments.FirstOrDefault();
            if (SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, request)
                && argument is { TypeKind: not TypeKind.Error }
                && !SymbolEqualityComparer.Default.Equals(argument.OriginalDefinition, result)
                && !SymbolEqualityComparer.Default.Equals(argument.OriginalDefinition, resultOfT))
            {
                context.ReportDiagnostic(Diagnostic.Create(NotAResult, type.Locations[0], type.ToDisplayString(), argument.ToDisplayString()));
            }
        }
    }
}

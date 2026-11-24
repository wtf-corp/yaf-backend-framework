using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace YAF.Analyzers.Ids;

/// <summary>
/// Reports <c>YAF1001</c> on each parameter of type <see cref="Guid"/> or <c>Guid?</c> named <c>id</c> or <c>*Id</c> of a method,
/// constructor or indexer that is public or protected in a public type (02 §3; plan 0006, Q4). Only in a compilation that references
/// YAF.Core. Generated code and the <c>From(Guid)</c> of a typed id are not reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class RawGuidIdAnalyzer : DiagnosticAnalyzer
{
    private const string TypedIdTypeName = "YAF.Core.Ids.ITypedId`1";

    public static readonly DiagnosticDescriptor RawGuidId = new(
        "YAF1001",
        "Raw Guid id parameter",
        "The parameter '{0}' of '{1}' is a raw Guid; use a typed id",
        "YAF.Ids",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A raw Guid lets the id of one aggregate be passed where another's is expected. Declare a typed id with [TypedId].");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [RawGuidId];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var guid = start.Compilation.GetTypeByMetadataName("System.Guid");
            var typedId = start.Compilation.GetTypeByMetadataName(TypedIdTypeName);
            if (guid is null || typedId is null)
            {
                return;
            }

            start.RegisterSymbolAction(symbol => Analyze(symbol, guid, typedId), SymbolKind.Method, SymbolKind.Property);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol guid, INamedTypeSymbol typedId)
    {
        var member = context.Symbol;
        var parameters = member switch
        {
            // Accessors are skipped: an indexer's parameters are reported once, on the indexer.
            IMethodSymbol { AssociatedSymbol: null } method when !IsTypedIdFrom(method, typedId) => method.Parameters,
            IPropertySymbol { IsIndexer: true } indexer => indexer.Parameters,
            _ => ImmutableArray<IParameterSymbol>.Empty,
        };

        if (parameters.IsEmpty || member.IsImplicitlyDeclared || !IsVisible(member))
        {
            return;
        }

        foreach (var parameter in parameters)
        {
            if (IsIdName(parameter.Name) && IsGuid(parameter.Type, guid))
            {
                context.ReportDiagnostic(Diagnostic.Create(RawGuidId, parameter.Locations[0], parameter.Name, member.ToDisplayString()));
            }
        }
    }

    private static bool IsIdName(string name) => name == "id" || name.EndsWith("Id", StringComparison.Ordinal);

    private static bool IsGuid(ITypeSymbol type, INamedTypeSymbol guid) =>
        SymbolEqualityComparer.Default.Equals(type, guid)
        || (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            && SymbolEqualityComparer.Default.Equals(nullable.TypeArguments[0], guid));

    // The static From of a type that implements ITypedId<TSelf> for itself.
    private static bool IsTypedIdFrom(IMethodSymbol method, INamedTypeSymbol typedId) =>
        method is { Name: "From", IsStatic: true }
        && method.ContainingType.AllInterfaces.Any(contract =>
            SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, typedId)
            && SymbolEqualityComparer.Default.Equals(contract.TypeArguments[0], method.ContainingType));

    // Public or protected, in a type that is visible outside its assembly.
    private static bool IsVisible(ISymbol member)
    {
        for (var symbol = member; symbol is not null and not INamespaceSymbol; symbol = symbol.ContainingType)
        {
            if (symbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
            {
                return false;
            }
        }

        return true;
    }
}

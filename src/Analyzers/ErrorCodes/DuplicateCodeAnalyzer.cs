using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace YAF.Analyzers.ErrorCodes;

/// <summary>
/// Reports <c>YAF3002</c> on each error field of an error catalogue whose code another field of the same compilation also has, naming the
/// other fields (03 §2). The codes are those of the generator (<see cref="ErrorCodeNaming.Code"/>); a field whose default code cannot be
/// computed is left to <c>YAF3004</c>. A field initialised by <c>new ErrorDescriptor(…)</c> counts with its constant code
/// (<see cref="ErrorCodeNaming.ConstructorField"/>); one whose code is not a constant is not checked (plan 0008, T4).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class DuplicateCodeAnalyzer : DiagnosticAnalyzer
{
    private static readonly SymbolDisplayFormat s_fieldFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType);

    public static readonly DiagnosticDescriptor DuplicateCode = new(
        "YAF3002",
        "Two errors have the same code",
        "The code '{0}' of error '{1}' is also the code of '{2}'",
        "YAF.Errors",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Clients match on codes, so each code is declared once. Rename one of the fields or give it a different [ErrorCode].",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [DuplicateCode];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var serviceCode = ErrorCodeNaming.ServiceCode(start.Options.AnalyzerConfigOptionsProvider.GlobalOptions);
            var fields = new ConcurrentBag<(string Code, IFieldSymbol Field)>();
            start.RegisterSyntaxNodeAction(node => Collect(node, serviceCode, fields), SyntaxKind.InvocationExpression);
            start.RegisterSyntaxNodeAction(
                node => CollectCreation(node, fields), SyntaxKind.ObjectCreationExpression, SyntaxKind.ImplicitObjectCreationExpression);
            start.RegisterCompilationEndAction(end => Report(end, fields));
        });
    }

    private static void Collect(SyntaxNodeAnalysisContext context, string? serviceCode, ConcurrentBag<(string Code, IFieldSymbol Field)> fields)
    {
        if (ErrorCodeNaming.IsCandidate(context.Node)
            && ErrorCodeNaming.CatalogueField((InvocationExpressionSyntax)context.Node, context.SemanticModel, context.CancellationToken)
                is { } field
            && ErrorCodeNaming.Code(field, serviceCode) is { } code)
        {
            fields.Add((code, field));
        }
    }

    private static void CollectCreation(SyntaxNodeAnalysisContext context, ConcurrentBag<(string Code, IFieldSymbol Field)> fields)
    {
        if (ErrorCodeNaming.ConstructorField((BaseObjectCreationExpressionSyntax)context.Node, context.SemanticModel, context.CancellationToken)
            is { } declared)
        {
            fields.Add((declared.Code, declared.Field));
        }
    }

    private static void Report(CompilationAnalysisContext context, ConcurrentBag<(string Code, IFieldSymbol Field)> fields)
    {
        foreach (var group in fields.GroupBy(static entry => entry.Code, StringComparer.Ordinal).Where(static group => group.Count() > 1))
        {
            foreach (var (code, field) in group)
            {
                var others = group
                    .Where(other => !SymbolEqualityComparer.Default.Equals(other.Field, field))
                    .Select(static other => other.Field.ToDisplayString(s_fieldFormat))
                    .OrderBy(static name => name, StringComparer.Ordinal);
                context.ReportDiagnostic(
                    Diagnostic.Create(DuplicateCode, field.Locations[0], code, field.ToDisplayString(s_fieldFormat), string.Join("', '", others)));
            }
        }
    }
}

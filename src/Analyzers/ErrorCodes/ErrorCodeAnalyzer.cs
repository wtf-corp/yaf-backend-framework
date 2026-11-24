using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace YAF.Analyzers.ErrorCodes;

/// <summary>
/// Reports <c>YAF3004</c> on a factory call of an error catalogue whose code cannot be computed or breaks the rules of 03 §2: a default
/// code while <c>YafServiceCode</c> is missing or not <c>[a-z0-9_]+</c>, or a computed or <c>[ErrorCode]</c> code that is not
/// <c>[a-z0-9_.]</c>, starts or ends with a dot, or is longer than 100 characters (plan 0006, Q5).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class ErrorCodeAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor InvalidCode = new(
        "YAF3004",
        "Error code cannot be computed or is invalid",
        "The code of error '{0}' {1}",
        "YAF.Errors",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Codes are lower-case [a-z0-9_.], do not start or end with a dot and have at most 100 characters. "
            + "A default code needs the MSBuild property YafServiceCode, matching [a-z0-9_]+.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [InvalidCode];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var serviceCode = ErrorCodeNaming.ServiceCode(start.Options.AnalyzerConfigOptionsProvider.GlobalOptions);
            start.RegisterSyntaxNodeAction(node => Analyze(node, serviceCode), SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, string? serviceCode)
    {
        if (!ErrorCodeNaming.IsCandidate(context.Node)
            || ErrorCodeNaming.CatalogueField((InvocationExpressionSyntax)context.Node, context.SemanticModel, context.CancellationToken)
                is not { } field)
        {
            return;
        }

        var problem = Problem(field, serviceCode);
        if (problem is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidCode, context.Node.GetLocation(), field.Name, problem));
        }
    }

    private static string? Problem(IFieldSymbol field, string? serviceCode)
    {
        var code = ErrorCodeNaming.ExplicitCode(field);
        if (code is null)
        {
            if (serviceCode is null)
            {
                return "needs the MSBuild property YafServiceCode, which is missing";
            }

            if (!ErrorCodeNaming.IsValidServiceCode(serviceCode))
            {
                return $"needs the MSBuild property YafServiceCode to match [a-z0-9_]+, but it is '{serviceCode}'";
            }

            code = serviceCode + "." + ErrorCodeNaming.DefaultPath(field);
        }

        return ErrorCodeNaming.IsValidCode(code)
            ? null
            : $"is '{code}', which is not lower-case [a-z0-9_.], starts or ends with a dot, or is longer than {ErrorCodeNaming.MaxCodeLength} characters";
    }
}

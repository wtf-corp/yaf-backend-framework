using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace YAF.Analyzers.ErrorCodes;

/// <summary>
/// Reports <c>YAF3001</c> on <c>new Error(…)</c>, <c>new ErrorDescriptor(…)</c> or a factory call of <c>Error</c> anywhere other than the
/// initialiser of a static field of type <c>Error</c> or <c>ErrorDescriptor</c> of an <c>IErrorCatalog</c> type (03 §2), whatever
/// conversion leads from the creation to the field. Creation inside <c>Error</c> and <c>ErrorDescriptor</c>
/// themselves (<c>ToError</c>, <c>With</c>, <c>At</c>) and generated code are not reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class CatalogueAnalyzer : DiagnosticAnalyzer
{
    private const string DescriptorTypeName = "YAF.Core.Results.ErrorDescriptor";

    public static readonly DiagnosticDescriptor OutsideCatalogue = new(
        "YAF3001",
        "Error created outside an error catalogue",
        "'{0}' is created outside the initialiser of a static field of type Error or ErrorDescriptor of an IErrorCatalog type",
        "YAF.Errors",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Declare each error once, as a static readonly field of type ErrorDescriptor or Error of an IErrorCatalog type "
            + "initialised by a factory of Error, and create the error from it with ToError, With or At.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [OutsideCatalogue];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeCreation, OperationKind.ObjectCreation);
        context.RegisterOperationAction(AnalyzeFactoryCall, OperationKind.Invocation);
    }

    private static void AnalyzeCreation(OperationAnalysisContext context)
    {
        var type = ((IObjectCreationOperation)context.Operation).Type;
        if (type?.ToDisplayString() is ErrorCodeNaming.ErrorTypeName or DescriptorTypeName)
        {
            Check(context, "new " + type.Name);
        }
    }

    private static void AnalyzeFactoryCall(OperationAnalysisContext context)
    {
        var method = ((IInvocationOperation)context.Operation).TargetMethod;
        if (method.IsStatic
            && method.ContainingType.ToDisplayString() == ErrorCodeNaming.ErrorTypeName
            && ErrorCodeNaming.FactoryNames.Contains(method.Name))
        {
            Check(context, "Error." + method.Name);
        }
    }

    private static void Check(OperationAnalysisContext context, string created)
    {
        if (!InErrorType(context.ContainingSymbol) && !InitialisesCatalogueField(context.Operation))
        {
            context.ReportDiagnostic(Diagnostic.Create(OutsideCatalogue, context.Operation.Syntax.GetLocation(), created));
        }
    }

    private static bool InErrorType(ISymbol symbol)
    {
        for (var type = symbol as INamedTypeSymbol ?? symbol.ContainingType; type is not null; type = type.ContainingType)
        {
            if (type.ToDisplayString() is ErrorCodeNaming.ErrorTypeName or DescriptorTypeName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool InitialisesCatalogueField(IOperation operation)
    {
        var parent = operation.Parent;
        while (parent is IConversionOperation { IsImplicit: true })
        {
            parent = parent.Parent;
        }

        return parent is IFieldInitializerOperation initializer
        && initializer.InitializedFields.All(static field =>
            field.IsStatic
            && field.Type.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString()
                is ErrorCodeNaming.ErrorTypeName or DescriptorTypeName
            && field.ContainingType.AllInterfaces.Any(static type => type.ToDisplayString() == ErrorCodeNaming.CatalogueTypeName));
    }
}

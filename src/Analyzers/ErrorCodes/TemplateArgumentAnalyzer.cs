using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace YAF.Analyzers.ErrorCodes;

/// <summary>
/// Reports <c>YAF3003</c> at <c>descriptor.With(("Name", value), …)</c> for each <c>{Name}</c> placeholder of the descriptor's message
/// template that has no argument of that name (03 §2). Only when the descriptor is a field of the same compilation whose template is a
/// constant and every argument name is a constant; otherwise nothing is reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class TemplateArgumentAnalyzer : DiagnosticAnalyzer
{
    private const string DescriptorTypeName = "YAF.Core.Results.ErrorDescriptor";

    // The parameter of the ErrorDescriptor constructor that takes the template; arguments are matched by it, never by position.
    private const string TemplateParameterName = "MessageTemplate";

    private static readonly SymbolDisplayFormat s_fieldFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType);

    public static readonly DiagnosticDescriptor MissingArgument = new(
        "YAF3003",
        "Message placeholder without an argument",
        "The placeholder '{{{0}}}' of the message template of '{1}' has no argument",
        "YAF.Errors",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A placeholder without an argument stays in the message as it is. Pass an argument of that name to With.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [MissingArgument];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var templates = new ConcurrentDictionary<IFieldSymbol, string>(SymbolEqualityComparer.Default);
            var calls = new ConcurrentBag<(IFieldSymbol Field, ImmutableArray<string> Names, Location Location)>();
            start.RegisterOperationAction(operation => CollectTemplate(operation, templates), OperationKind.FieldInitializer);
            start.RegisterOperationAction(operation => CollectCall(operation, calls), OperationKind.Invocation);
            start.RegisterCompilationEndAction(end => Report(end, templates, calls));
        });
    }

    private static void CollectTemplate(OperationAnalysisContext context, ConcurrentDictionary<IFieldSymbol, string> templates)
    {
        var initializer = (IFieldInitializerOperation)context.Operation;
        var template = Unwrap(initializer.Value) switch
        {
            IInvocationOperation { TargetMethod: { IsStatic: true } method, Arguments: { Length: 1 } arguments }
                when method.ContainingType.ToDisplayString() == ErrorCodeNaming.ErrorTypeName
                && ErrorCodeNaming.FactoryNames.Contains(method.Name) => arguments[0].Value.ConstantValue,
            IObjectCreationOperation { Arguments: var arguments } creation
                when creation.Type?.ToDisplayString() == DescriptorTypeName
                => arguments.FirstOrDefault(argument => argument.Parameter?.Name == TemplateParameterName)?.Value.ConstantValue ?? default,
            _ => default,
        };

        if (template is { HasValue: true, Value: string text })
        {
            foreach (var field in initializer.InitializedFields)
            {
                templates[field] = text;
            }
        }
    }

    private static void CollectCall(
        OperationAnalysisContext context, ConcurrentBag<(IFieldSymbol Field, ImmutableArray<string> Names, Location Location)> calls)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod is { Name: "With", IsStatic: false } method
            && method.ContainingType.ToDisplayString() == DescriptorTypeName
            && invocation.Instance is IFieldReferenceOperation { Field: var field }
            && invocation.Arguments.Length == 1
            && ConstantNames(invocation.Arguments[0].Value) is { IsDefault: false } names)
        {
            calls.Add((field, names, invocation.Syntax.GetLocation()));
        }
    }

    // The names of the arguments of With, or default when one of them is not a constant or the arguments are not written out.
    private static ImmutableArray<string> ConstantNames(IOperation arguments)
    {
        var elements = Unwrap(arguments) switch
        {
            IArrayCreationOperation { Initializer: { } initializer } => initializer.ElementValues,
            ICollectionExpressionOperation collection => collection.Elements,
            _ => default,
        };

        if (elements.IsDefault)
        {
            return default;
        }

        var names = ImmutableArray.CreateBuilder<string>(elements.Length);
        foreach (var element in elements)
        {
            if (Unwrap(element) is not ITupleOperation { Elements: { Length: 2 } pair }
                || Unwrap(pair[0]).ConstantValue is not { HasValue: true, Value: string text })
            {
                return default;
            }

            names.Add(text);
        }

        return names.MoveToImmutable();
    }

    private static void Report(
        CompilationAnalysisContext context,
        ConcurrentDictionary<IFieldSymbol, string> templates,
        ConcurrentBag<(IFieldSymbol Field, ImmutableArray<string> Names, Location Location)> calls)
    {
        foreach (var (field, names, location) in calls)
        {
            if (!templates.TryGetValue(field, out var template))
            {
                continue;
            }

            foreach (var placeholder in Placeholders(template).Where(placeholder => !names.Contains(placeholder, StringComparer.Ordinal)))
            {
                context.ReportDiagnostic(Diagnostic.Create(MissingArgument, location, placeholder, field.ToDisplayString(s_fieldFormat)));
            }
        }
    }

    // The placeholder names as ErrorDescriptor.With finds them: from each '{' to the next '}', in order, each once.
    private static List<string> Placeholders(string template)
    {
        var found = new List<string>();
        var position = 0;
        while (position < template.Length)
        {
            var open = template.IndexOf('{', position);
            var close = open < 0 ? -1 : template.IndexOf('}', open + 1);
            if (close < 0)
            {
                break;
            }

            var name = template.Substring(open + 1, close - open - 1);
            if (!found.Contains(name, StringComparer.Ordinal))
            {
                found.Add(name);
            }

            position = close + 1;
        }

        return found;
    }

    private static IOperation Unwrap(IOperation operation)
    {
        while (operation is IConversionOperation { IsImplicit: true } conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }
}

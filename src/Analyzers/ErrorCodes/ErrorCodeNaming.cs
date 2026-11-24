using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace YAF.Analyzers.ErrorCodes;

/// <summary>
/// Finds the factory calls of <c>YAF.Core.Results.Error</c> that declare an error field of an error catalogue, and computes their codes
/// (specs/0000-framework/03-results-and-errors.md §2). Shared by the generator and the diagnostics on error codes.
/// </summary>
internal static class ErrorCodeNaming
{
    public const string ErrorTypeName = "YAF.Core.Results.Error";
    public const string CatalogueTypeName = "YAF.Core.Results.IErrorCatalog";
    public const string ErrorCodeAttributeName = "YAF.Core.Results.ErrorCodeAttribute";

    /// <summary>The nine factories, one per member of <c>ErrorKind</c> and named like it.</summary>
    public static readonly string[] FactoryNames =
        ["Validation", "Unauthenticated", "Forbidden", "NotFound", "Conflict", "PreconditionFailed", "RateLimited", "Unavailable", "Failure"];

    /// <summary>The longest code allowed (03 §2).</summary>
    public const int MaxCodeLength = 100;

    /// <summary>A factory call by name that initialises a variable: worth asking the semantic model about.</summary>
    public static bool IsCandidate(SyntaxNode node) =>
        node is InvocationExpressionSyntax { Parent: EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax } } invocation
        && FactoryName(invocation) is { } name
        && FactoryNames.Contains(name);

    /// <summary>The name of the invoked method as written, or <see langword="null"/>.</summary>
    public static string? FactoryName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => null,
    };

    /// <summary>
    /// The static field of an error catalogue that a candidate call of a factory of <c>Error</c> initialises, or <see langword="null"/>.
    /// </summary>
    public static IFieldSymbol? CatalogueField(InvocationExpressionSyntax invocation, SemanticModel model, CancellationToken token)
    {
        if (model.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol { IsStatic: true } method
            || method.ContainingType.ToDisplayString() != ErrorTypeName
            || model.GetDeclaredSymbol(invocation.Parent!.Parent!, token) is not IFieldSymbol { IsStatic: true } field
            || !field.ContainingType.AllInterfaces.Any(static type => type.ToDisplayString() == CatalogueTypeName))
        {
            return null;
        }

        return field;
    }

    /// <summary>The MSBuild property <c>YafServiceCode</c>, trimmed, or <see langword="null"/> when it is missing or blank.</summary>
    public static string? ServiceCode(AnalyzerConfigOptions options) =>
        options.TryGetValue("build_property.YafServiceCode", out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    /// <summary>A service code is one segment of a code: <c>[a-z0-9_]+</c>.</summary>
    public static bool IsValidServiceCode(string serviceCode) =>
        serviceCode.Length > 0 && serviceCode.All(static c => IsLower(c) || IsDigit(c) || c == '_');

    /// <summary>A code is <c>[a-z0-9_.]</c>, does not start or end with a dot and has at most 100 characters (03 §2).</summary>
    public static bool IsValidCode(string code) =>
        code.Length is > 0 and <= MaxCodeLength
        && code[0] != '.'
        && code[code.Length - 1] != '.'
        && code.All(static c => IsLower(c) || IsDigit(c) || c is '_' or '.');

    /// <summary>
    /// The default code without the service code: the containing types of the catalogue, outermost first, without namespace and type
    /// parameters, then the field, each in snake case and joined with dots (<c>Session.Errors.Full</c> gives <c>session.full</c>).
    /// </summary>
    public static string DefaultPath(IFieldSymbol field)
    {
        var parts = new List<string> { ToSnakeCase(field.Name) };
        for (var type = field.ContainingType.ContainingType; type is not null; type = type.ContainingType)
        {
            parts.Insert(0, ToSnakeCase(type.Name));
        }

        return string.Join(".", parts);
    }

    /// <summary>
    /// The code of the field: the code of <c>[ErrorCode]</c>, else <c>&lt;serviceCode&gt;.&lt;default path&gt;</c>, or <see langword="null"/>
    /// when a default code is needed and the service code is missing or invalid (<c>YAF3004</c> reports that).
    /// </summary>
    public static string? Code(IFieldSymbol field, string? serviceCode) =>
        ExplicitCode(field)
        ?? (serviceCode is not null && IsValidServiceCode(serviceCode) ? serviceCode + "." + DefaultPath(field) : null);

    /// <summary>The code given by <c>[ErrorCode]</c> on the field, or <see langword="null"/> when there is none.</summary>
    public static string? ExplicitCode(IFieldSymbol field)
    {
        foreach (var attribute in field.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == ErrorCodeAttributeName
                && attribute.ConstructorArguments.Length == 1)
            {
                return attribute.ConstructorArguments[0].Value as string;
            }
        }

        return null;
    }

    /// <summary>
    /// Splits an identifier into lower-case words joined by <c>_</c>: a break before an upper-case letter that follows a lower-case letter
    /// or a digit, and before the last capital of a run that is followed by a lower-case letter (<c>HTTPRequest</c> gives
    /// <c>http_request</c>). Only ASCII letters are lowered; anything else is kept as it is.
    /// </summary>
    public static string ToSnakeCase(string identifier)
    {
        var builder = new StringBuilder(identifier.Length + 4);
        for (var i = 0; i < identifier.Length; i++)
        {
            var current = identifier[i];
            if (IsUpper(current) && i > 0 && StartsWord(identifier, i))
            {
                builder.Append('_');
            }

            builder.Append(IsUpper(current) ? (char)(current - 'A' + 'a') : current);
        }

        return builder.ToString();
    }

    private static bool StartsWord(string identifier, int index)
    {
        var previous = identifier[index - 1];
        var nextIsLower = index + 1 < identifier.Length && IsLower(identifier[index + 1]);
        return IsLower(previous) || IsDigit(previous) || (IsUpper(previous) && nextIsLower);
    }

    private static bool IsUpper(char c) => c is >= 'A' and <= 'Z';

    private static bool IsLower(char c) => c is >= 'a' and <= 'z';

    private static bool IsDigit(char c) => c is >= '0' and <= '9';
}

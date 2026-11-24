using System.Text;
using Microsoft.CodeAnalysis;

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

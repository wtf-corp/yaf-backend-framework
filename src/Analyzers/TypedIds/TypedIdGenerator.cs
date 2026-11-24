using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace YAF.Analyzers.TypedIds;

/// <summary>
/// Emits the members of every <c>[TypedId]</c> <c>readonly partial record struct</c> that 02 §3 lists: the
/// <c>ITypedId&lt;TSelf&gt;</c> implementation, <c>Value</c>, <c>New()</c>, <c>From(Guid)</c>, <c>IParsable&lt;TSelf&gt;</c>,
/// <c>ToString()</c> in format <c>D</c>, and a JSON and a type converter. Nothing EF-related.
/// </summary>
/// <remarks>Only top-level ids are generated: an id is declared next to its aggregate, not nested in a type.</remarks>
[Generator(LanguageNames.CSharp)]
internal sealed class TypedIdGenerator : IIncrementalGenerator
{
    private const string AttributeName = "YAF.Core.Ids.TypedIdAttribute";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var ids = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeName,
            static (node, _) => node is RecordDeclarationSyntax record
                && record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
                && record.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax,
            static (attributeContext, _) => ToModel(attributeContext.TargetSymbol));

        context.RegisterSourceOutput(ids, static (production, id) => production.AddSource(id.HintName, TypedIdSource.Write(id)));
    }

    private static TypedIdModel ToModel(ISymbol symbol) => new(
        symbol.ContainingNamespace.IsGlobalNamespace ? null : symbol.ContainingNamespace.ToDisplayString(),
        symbol.Name);
}

namespace YAF.Core.Ids;

/// <summary>
/// Declares a typed identifier. The generator shipped in <c>YAF.Core</c> emits the rest of the type: the <see cref="ITypedId{TSelf}"/>
/// implementation, <c>Value</c>, <c>New()</c>, <c>From(Guid)</c>, parsing, <c>ToString()</c> and the JSON and type converters.
/// </summary>
/// <remarks>
/// Apply it to a <c>readonly partial record struct</c> with no members, next to the aggregate it identifies. One id type per aggregate type.
/// </remarks>
/// <example>
/// <code language="csharp">
/// [TypedId]
/// public readonly partial record struct SessionId;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class TypedIdAttribute : Attribute;

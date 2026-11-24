namespace YAF.Core.Results;

/// <summary>
/// Pins the code of an error field of an <see cref="IErrorCatalog"/>, replacing the default code the error code generator computes.
/// Use it for a code clients depend on, so that renaming the field or its declaring type does not change the code.
/// </summary>
/// <remarks>
/// The code must be lower-case <c>[a-z0-9_.]</c> and at most 100 characters; it replaces the whole default code, service code included.
/// </remarks>
/// <example>
/// <code language="csharp">
/// [ErrorCode("catalog.session.title_required")]
/// public static readonly ErrorDescriptor TitleRequired = Error.Validation("Title is required.");
/// </code>
/// </example>
/// <param name="code">The full error code, for example <c>catalog.session.title_required</c>.</param>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class ErrorCodeAttribute(string code) : Attribute
{
    /// <summary>Gets the full error code given to the constructor.</summary>
    public string Code { get; } = code;
}

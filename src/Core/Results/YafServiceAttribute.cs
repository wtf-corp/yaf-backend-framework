using System.Diagnostics.CodeAnalysis;

namespace YAF.Core.Results;

/// <summary>
/// Names the service an assembly belongs to and the generated <see cref="IErrorRegistry"/> that lists the errors it declares, so that
/// the error catalogue can be read without scanning types.
/// </summary>
/// <remarks>
/// Do not apply it by hand: the error registry generator emits it, together with the registry, in a compilation that sets the MSBuild
/// property <c>YafServiceCode</c> and declares at least one <see cref="IErrorCatalog"/> field. The registry must be a class with a public
/// parameterless constructor; the reader creates one instance of it.
/// </remarks>
/// <example>
/// <code language="csharp">
/// [assembly: YafService("catalog", typeof(YAF.Generated.YafErrorRegistry))]
/// </code>
/// </example>
/// <param name="serviceCode">The service code of the assembly, the value of <c>YafServiceCode</c>, for example <c>catalog</c>.</param>
/// <param name="errorRegistry">The type of the generated registry, which implements <see cref="IErrorRegistry"/>.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class YafServiceAttribute(
    string serviceCode,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type errorRegistry) : Attribute
{
    /// <summary>Gets the service code given to the constructor.</summary>
    public string ServiceCode { get; } = serviceCode;

    /// <summary>Gets the type of the generated registry given to the constructor; trimming keeps its public parameterless constructor.</summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type ErrorRegistry { get; } = errorRegistry;
}

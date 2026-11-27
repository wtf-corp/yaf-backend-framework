using System.Diagnostics.CodeAnalysis;

namespace YAF.Application.Registration;

/// <summary>
/// Names the generated <see cref="IHandlerRegistry"/> of an assembly, so that
/// <see cref="YafApplicationBuilder.AddHandlersFrom{TMarker}"/> registers its handlers without scanning types.
/// </summary>
/// <remarks>
/// Emitted by the generator; do not apply it by hand. The registry must be a class with a public parameterless constructor; the reader
/// creates one instance of it.
/// </remarks>
/// <example>
/// <code language="csharp">
/// [assembly: YafHandlers(typeof(YAF.Generated.YafHandlerRegistry))]
/// </code>
/// </example>
/// <param name="registry">The type of the generated registry, which implements <see cref="IHandlerRegistry"/>.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class YafHandlersAttribute(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type registry) : Attribute
{
    /// <summary>Gets the type of the generated registry given to the constructor; trimming keeps its public parameterless constructor.</summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type Registry { get; } = registry;
}

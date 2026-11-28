using YAF.Core.Results;

namespace YAF.Application.Errors;

/// <summary>
/// The error catalogue of the application: every error declared by the loaded assemblies that carry a <see cref="YafServiceAttribute"/>,
/// YAF's own (service code <c>core</c>) included, read from the error registry each of them names. No types are scanned.
/// </summary>
/// <remarks>
/// <see cref="YafApplicationServiceCollectionExtensions.AddYafApplication"/> registers it as a singleton. The catalogue is read once, when the
/// provider is first resolved, from the assemblies loaded at that time and those whose handlers were added with
/// <see cref="YafApplicationBuilder.AddHandlersFrom{TMarker}"/>; an assembly loaded later is not listed. Resolving it throws
/// <see cref="InvalidOperationException"/> when an assembly's <see cref="YafServiceAttribute"/> names a type that is not an
/// <see cref="IErrorRegistry"/>.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var codes = services.GetRequiredService&lt;IErrorCatalogProvider&gt;().Errors.Select(entry => entry.Descriptor.Code);
/// </code>
/// </example>
public interface IErrorCatalogProvider
{
    /// <summary>Gets the errors, assembly by assembly, each assembly's in the order its registry lists them; never <see langword="null"/>.</summary>
    IReadOnlyList<ErrorCatalogEntry> Errors { get; }
}

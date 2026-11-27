using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace Analyzers.Tests;

/// <summary>
/// What <see cref="GeneratorHarness"/> produced: every diagnostic that is not hidden, and, when it emitted, the image and
/// the loaded snippet assembly. Disposing unloads the assembly.
/// </summary>
internal sealed class HarnessResult(
    ImmutableArray<Diagnostic> diagnostics,
    byte[]? image,
    AssemblyLoadContext? context,
    Assembly? assembly) : IDisposable
{
    public ImmutableArray<Diagnostic> Diagnostics { get; } = diagnostics;

    /// <summary>The emitted assembly, for a further compilation to reference with <c>MetadataReference.CreateFromImage</c>.</summary>
    public ImmutableArray<byte> Image { get; } = image is null ? [] : [.. image];

    public Assembly? Assembly { get; } = assembly;

    public void Dispose() => context?.Unload();
}

using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace Analyzers.Tests;

/// <summary>
/// What <see cref="GeneratorHarness"/> produced: every diagnostic that is not hidden, and the loaded snippet assembly
/// when it emitted. Disposing unloads the assembly.
/// </summary>
internal sealed class HarnessResult(ImmutableArray<Diagnostic> diagnostics, AssemblyLoadContext? context, Assembly? assembly) : IDisposable
{
    public ImmutableArray<Diagnostic> Diagnostics { get; } = diagnostics;

    public Assembly? Assembly { get; } = assembly;

    public void Dispose() => context?.Unload();
}

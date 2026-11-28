using System.Reflection;

namespace YAF.Application.Errors;

/// <summary>
/// Gives <see cref="ErrorCatalogProvider"/> the assemblies whose error catalogues it lists. The default, <see cref="LoadedAssemblySource"/>,
/// returns the assemblies loaded in the process; a test replaces it so that fixture assemblies are never loaded into the shared test process
/// (<c>specs/0013-thin-slice/plan.md</c>, T24 and T25).
/// </summary>
internal interface IErrorCatalogAssemblySource
{
    /// <summary>Returns the assemblies to read. One without a <c>YafService</c> attribute declares no catalogue and is skipped.</summary>
    /// <returns>The assemblies, in any order.</returns>
    IReadOnlyList<Assembly> GetAssemblies();
}

/// <summary>The assemblies loaded in the process when the catalogue is read.</summary>
internal sealed class LoadedAssemblySource : IErrorCatalogAssemblySource
{
    public IReadOnlyList<Assembly> GetAssemblies() => AppDomain.CurrentDomain.GetAssemblies();
}

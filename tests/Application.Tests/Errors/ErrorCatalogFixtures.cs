using System.Reflection;
using YAF.Application.Errors;
using YAF.Core.Results;

namespace Application.Tests.Errors;

/// <summary>A catalogue of this test assembly (service code <c>tests</c>), standing in for a service's own.</summary>
internal static class Listing
{
    internal abstract class Errors : IErrorCatalog
    {
        public static readonly ErrorDescriptor NotFound = Error.NotFound("The listing was not found.");
    }
}

/// <summary>Gives the catalogue exactly the assemblies a test names, in place of the assemblies loaded in the process.</summary>
internal sealed class FixedAssemblySource(params Assembly[] assemblies) : IErrorCatalogAssemblySource
{
    public IReadOnlyList<Assembly> GetAssemblies() => assemblies;
}

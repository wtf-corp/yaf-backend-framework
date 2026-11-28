using System.Reflection;
using YAF.Application.Registration;
using YAF.Core.Results;

namespace YAF.Application.Errors;

/// <summary>
/// Lists the errors of the source's assemblies and of the assemblies of the registered handler registries, so that a handler assembly is
/// not missed whatever the source returns (<c>03</c> §2.1). Each assembly's <see cref="YafServiceAttribute"/> names its generated
/// <see cref="IErrorRegistry"/>, which is created once; an assembly without the attribute is skipped.
/// </summary>
internal sealed class ErrorCatalogProvider(IErrorCatalogAssemblySource source, ApplicationRegistrations registrations) : IErrorCatalogProvider
{
    public IReadOnlyList<ErrorCatalogEntry> Errors { get; } = source.GetAssemblies()
        .Concat(registrations.Registries.Select(registry => registry.GetType().Assembly))
        .Distinct()
        .SelectMany(Read)
        .ToList();

    private static IEnumerable<ErrorCatalogEntry> Read(Assembly assembly)
    {
        if (assembly.GetCustomAttribute<YafServiceAttribute>() is not { } attribute)
        {
            return [];
        }

        if (Activator.CreateInstance(attribute.ErrorRegistry) is not IErrorRegistry registry)
        {
            throw new InvalidOperationException(
                $"The error registry {attribute.ErrorRegistry} named by assembly {assembly.GetName().Name} does not implement IErrorRegistry.");
        }

        return registry.Errors.Select(error => new ErrorCatalogEntry(error.Descriptor, error.DeclaringType));
    }
}

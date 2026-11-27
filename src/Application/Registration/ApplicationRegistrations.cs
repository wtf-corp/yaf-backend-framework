using System.Reflection;
using YAF.Application.Pipeline;

namespace YAF.Application.Registration;

/// <summary>What <c>AddYafApplication</c> has collected: one instance per service collection, so that a second call adds nothing twice.</summary>
internal sealed class ApplicationRegistrations
{
    private readonly HashSet<Assembly> _assemblies = [];
    private readonly List<IHandlerRegistry> _registries = [];

    public PipelineBuilder Pipeline { get; } = new();

    public IReadOnlyList<IHandlerRegistry> Registries => _registries;

    public bool TryAddAssembly(Assembly assembly) => _assemblies.Add(assembly);

    public void Add(IHandlerRegistry registry) => _registries.Add(registry);
}

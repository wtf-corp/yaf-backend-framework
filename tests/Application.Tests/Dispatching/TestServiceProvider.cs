using Microsoft.Extensions.DependencyInjection;

namespace Application.Tests.Dispatching;

/// <summary>
/// The smallest container the dispatcher tests need: the platform has the DI abstractions but no container, and this project takes no
/// package for one. Closed service types only; the last registration wins; singletons are kept by the root, other services by each scope.
/// </summary>
internal sealed class TestServiceProvider : IServiceProvider, IServiceScopeFactory, IServiceScope
{
    private readonly IReadOnlyList<ServiceDescriptor> _descriptors;
    private readonly TestServiceProvider? _root;
    private readonly Dictionary<ServiceDescriptor, object> _instances = [];

    private TestServiceProvider(IReadOnlyList<ServiceDescriptor> descriptors, TestServiceProvider? root)
    {
        _descriptors = descriptors;
        _root = root;
    }

    public IServiceProvider ServiceProvider => this;

    public static TestServiceProvider Build(IServiceCollection services) => new([.. services], root: null);

    public IServiceScope CreateScope() => new TestServiceProvider(_descriptors, _root ?? this);

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IServiceProvider) || serviceType == typeof(IServiceScopeFactory))
        {
            return this;
        }

        var descriptor = _descriptors.LastOrDefault(candidate => !candidate.IsKeyedService && candidate.ServiceType == serviceType);
        return descriptor?.Lifetime switch
        {
            null => null,
            ServiceLifetime.Singleton => (_root ?? this).Keep(descriptor),
            _ => _root is null ? throw new InvalidOperationException($"Resolve {serviceType} from a scope.") : Keep(descriptor),
        };
    }

    public void Dispose()
    {
    }

    private object Keep(ServiceDescriptor descriptor)
    {
        if (!_instances.TryGetValue(descriptor, out var instance))
        {
            instance = Create(descriptor, this);
            _instances.Add(descriptor, instance);
        }

        return instance;
    }

    private static object Create(ServiceDescriptor descriptor, IServiceProvider provider) =>
        descriptor.ImplementationInstance
        ?? descriptor.ImplementationFactory?.Invoke(provider)
        ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
}

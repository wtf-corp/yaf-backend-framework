using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using YAF.Application.Pipeline;
using YAF.Application.Registration;

namespace YAF.Application;

/// <summary>
/// Configures the application layer inside <see cref="YafApplicationServiceCollectionExtensions.AddYafApplication"/>: which assemblies'
/// handlers are registered and in which order the middleware runs.
/// </summary>
public sealed class YafApplicationBuilder
{
    private readonly IServiceCollection _services;
    private readonly ApplicationRegistrations _registrations;

    internal YafApplicationBuilder(IServiceCollection services, ApplicationRegistrations registrations)
    {
        _services = services;
        _registrations = registrations;
    }

    /// <summary>
    /// Registers the handlers of the assembly that declares <typeparamref name="TMarker"/>, from the registry the generator names in its
    /// <see cref="YafHandlersAttribute"/>. No types are scanned. Registering the same assembly again does nothing.
    /// </summary>
    /// <typeparam name="TMarker">Any type of the assembly whose handlers are registered.</typeparam>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">
    /// The assembly carries no <see cref="YafHandlersAttribute"/> (it does not reference the YAF analyzers), or the type it names is not an
    /// <see cref="IHandlerRegistry"/> with a public parameterless constructor.
    /// </exception>
    public YafApplicationBuilder AddHandlersFrom<TMarker>()
    {
        var assembly = typeof(TMarker).Assembly;
        if (!_registrations.TryAddAssembly(assembly))
        {
            return this;
        }

        var attribute = assembly.GetCustomAttribute<YafHandlersAttribute>()
            ?? throw new InvalidOperationException(
                $"Assembly {assembly.GetName().Name} has no [assembly: YafHandlers(...)] attribute. The handler registry generator emits it "
                + "in an assembly that references the YAF analyzers (through the YAF.Core package).");
        if (Activator.CreateInstance(attribute.Registry) is not IHandlerRegistry registry)
        {
            throw new InvalidOperationException(
                $"The handler registry {attribute.Registry} named by assembly {assembly.GetName().Name} does not implement IHandlerRegistry.");
        }

        registry.Register(_services);
        _registrations.Add(registry);
        return this;
    }

    /// <summary>Registers request middleware explicitly and in order, outermost first; later calls continue the same order.</summary>
    /// <param name="configure">Adds the steps to the pipeline.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">A step is not a middleware type.</exception>
    /// <exception cref="InvalidOperationException">An anchor of an insertion is not registered.</exception>
    public YafApplicationBuilder UsePipeline(Action<PipelineBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_registrations.Pipeline);
        return this;
    }
}

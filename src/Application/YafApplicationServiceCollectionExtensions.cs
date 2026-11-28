using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using YAF.Application.Pipeline;
using YAF.Application.Registration;
using YAF.Application.UnitOfWork;

namespace YAF.Application;

/// <summary>Adds the YAF application layer to a service collection.</summary>
public static class YafApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Adds the application layer: the dispatcher, the aggregate tracker and the domain event publisher (each per scope), the handlers of the assemblies the builder
    /// names, the pipeline order and a startup check that stops the host when a request type has no handler or more than one. Calling it again adds to the same
    /// configuration and registers nothing twice.
    /// </summary>
    /// <remarks>
    /// <see cref="TimeProvider.System"/> is registered only when no <see cref="TimeProvider"/> is registered yet. An <see cref="IDispatcher"/> registered afterwards
    /// replaces YAF's for application code; domain events are still published through YAF's dispatcher.
    /// </remarks>
    /// <param name="services">The application's services.</param>
    /// <param name="configure">Names the handler assemblies and the pipeline.</param>
    /// <returns><paramref name="services"/>.</returns>
    /// <exception cref="InvalidOperationException">A named assembly has no generated handler registry.</exception>
    /// <example>
    /// <code language="csharp">
    /// builder.Services.AddYafApplication(app => app
    ///     .AddHandlersFrom&lt;CreateSessionCommand&gt;()
    ///     .UsePipeline(p => p.Use(typeof(TenantQuotaMiddleware&lt;,&gt;))));
    /// </code>
    /// </example>
    public static IServiceCollection AddYafApplication(this IServiceCollection services, Action<YafApplicationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var registrations = services
            .Where(descriptor => descriptor.ServiceType == typeof(ApplicationRegistrations) && !descriptor.IsKeyedService)
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<ApplicationRegistrations>()
            .FirstOrDefault();
        if (registrations is null)
        {
            registrations = new ApplicationRegistrations();
            services.AddSingleton(registrations);
            services.AddSingleton<IHostedService>(new HandlerStartupCheck(registrations));
            services.AddSingleton<IPipelineDescriptor>(_ => registrations.Pipeline.Build());
            services.AddScoped<Dispatcher>();
            services.AddScoped<IDispatcher>(provider => provider.GetRequiredService<Dispatcher>());
            services.AddScoped<IAggregateTracker, AggregateTracker>();
            services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();
            services.TryAddSingleton(TimeProvider.System);
        }

        configure(new YafApplicationBuilder(services, registrations));
        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using YAF.Application.Context;
using YAF.Persistence.EfCore.Conventions;
using YAF.Persistence.EfCore.Interceptors;
using YAF.Persistence.EfCore.Tenancy;

namespace YAF.Persistence.EfCore;

/// <summary>Registers a <see cref="YafDbContext"/> with YAF's conventions.</summary>
public static class YafDbContextServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TContext"/> per scope, with YAF's conventions, the interceptors that stamp the rows of a save and enforce
    /// their tenant, and the <see cref="IYafDbContextServices"/> it is created with. Calling it again for the same context registers nothing and does not run
    /// <paramref name="configure"/>.
    /// </summary>
    /// <typeparam name="TContext">The service's context.</typeparam>
    /// <param name="services">The application's services. They must also hold an <see cref="IExecutionContextAccessor"/> and a
    /// <see cref="TimeProvider"/>; resolving the context fails without them.</param>
    /// <param name="configure">Chooses the database provider and its options.</param>
    /// <returns><paramref name="services"/>.</returns>
    /// <remarks>
    /// The model of the context fails to build, with an <see cref="InvalidOperationException"/> at its first use, when its
    /// <c>OnModelCreating</c> does not call <see cref="YafModelBuilderExtensions.ApplyYafModel"/> or a <see langword="string"/> property has
    /// no maximum length; the message names the property. A save throws <see cref="MissingExecutionContextException"/> when the scope has no
    /// execution context. An added tenant-owned row without a tenant gets the context's tenant; a save throws
    /// <see cref="InvalidOperationException"/>, naming the entity type, when an added or modified tenant-owned row belongs to another tenant
    /// or the context has no tenant.
    /// <para>
    /// The call throws <see cref="InvalidOperationException"/>, naming the context, when <typeparamref name="TContext"/> is already
    /// registered without it, for example by a plain <c>AddDbContext</c>: that registration would lack YAF's conventions, interceptors and
    /// model check. Register the context with this method alone.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// builder.Services.AddYafDbContext&lt;CatalogDbContext&gt;(options => options.UseNpgsql(connectionString));
    /// </code>
    /// </example>
    public static IServiceCollection AddYafDbContext<TContext>(this IServiceCollection services, Action<DbContextOptionsBuilder> configure)
        where TContext : YafDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(Registered<TContext>)))
        {
            return services;
        }

        if (services.Any(descriptor => descriptor.ServiceType == typeof(TContext)))
        {
            throw new InvalidOperationException(
                $"{typeof(TContext).FullName} is already registered without AddYafDbContext, so YAF's conventions, interceptors and model "
                + "check would not apply to it: register it with AddYafDbContext alone.");
        }

        services.AddSingleton(new Registered<TContext>());
        services.TryAddScoped<IYafDbContextServices, YafDbContextServices>();
        services.AddDbContext<TContext>((provider, options) =>
        {
            var yafServices = provider.GetRequiredService<IYafDbContextServices>();
            options.AddInterceptors(new StampingInterceptor(yafServices), new TenantInterceptor(yafServices));
            configure(options);
            ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension(new YafDbContextOptionsExtension());
        });
        return services;
    }

    // Registered once per context type by AddYafDbContext; a registration of the context without it was made some other way.
    private sealed class Registered<TContext>;

    private sealed class YafDbContextServices(IExecutionContextAccessor executionContext, TimeProvider timeProvider) : IYafDbContextServices
    {
        public IExecutionContextAccessor ExecutionContext { get; } = executionContext;

        public TimeProvider TimeProvider { get; } = timeProvider;
    }
}

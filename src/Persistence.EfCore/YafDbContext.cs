using Microsoft.EntityFrameworkCore;
using YAF.Persistence.EfCore.Conventions;

namespace YAF.Persistence.EfCore;

/// <summary>The base of a service's EF Core context: it carries the services YAF's conventions and interceptors read.</summary>
/// <remarks>
/// Register the derived context with <see cref="YafDbContextServiceCollectionExtensions.AddYafDbContext{TContext}"/>, which says when its
/// model fails to build, and call <see cref="YafModelBuilderExtensions.ApplyYafModel"/> last in <see cref="DbContext.OnModelCreating"/>.
/// </remarks>
/// <param name="options">The options of the derived context, as dependency injection gives them.</param>
/// <param name="services">The services the context needs.</param>
/// <example>
/// <code language="csharp">
/// internal sealed class CatalogDbContext(DbContextOptions&lt;CatalogDbContext&gt; options, IYafDbContextServices services)
///     : YafDbContext(options, services)
/// {
///     protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyYafModel(this);
/// }
/// </code>
/// </example>
public abstract class YafDbContext(DbContextOptions options, IYafDbContextServices services) : DbContext(options)
{
    /// <summary>Gets the services the context was created with.</summary>
    protected IYafDbContextServices Services { get; } = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// Converts the typed members of the data classes declared in the derived context's assembly: a typed id to its <see cref="Guid"/>,
    /// a smart enumeration to its code and an enum to its name (both at most 64 characters), <c>Currency</c> to its code,
    /// <c>EmailAddress</c> to its value (at most 254 characters) and <c>Percentage</c> to its value, each also in its nullable form.
    /// </summary>
    /// <param name="configurationBuilder">The builder of the conventions of the context's model.</param>
    /// <remarks>
    /// A derived context that overrides it calls this implementation. <c>Money</c>, <c>Period</c> and <c>DateRange</c> are mapped by
    /// <see cref="YafModelBuilderExtensions.ApplyYafModel"/>. An app's own value objects are mapped in its entity configuration.
    /// </remarks>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        TypedMemberConventions.Configure(configurationBuilder, GetType().Assembly);
    }
}

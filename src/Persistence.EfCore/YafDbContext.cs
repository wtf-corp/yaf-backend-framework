using Microsoft.EntityFrameworkCore;

namespace YAF.Persistence.EfCore;

/// <summary>The base of a service's EF Core context: it carries the services YAF's conventions and interceptors read.</summary>
/// <remarks>
/// Register the derived context with <see cref="YafDbContextServiceCollectionExtensions.AddYafDbContext{TContext}"/>, and call
/// <see cref="YafModelBuilderExtensions.ApplyYafModel"/> last in <see cref="DbContext.OnModelCreating"/>. The model then fails to build,
/// with an <see cref="InvalidOperationException"/> at its first use, when that call is missing or a <see langword="string"/> property
/// has no maximum length.
/// </remarks>
/// <param name="options">The options of the derived context, as dependency injection gives them.</param>
/// <param name="services">The services the context needs.</param>
/// <example>
/// <code language="csharp">
/// internal sealed class CatalogDbContext(DbContextOptions&lt;CatalogDbContext&gt; options, IYafDbContextServices services)
///     : YafDbContext(options, services)
/// {
///     protected override void OnModelCreating(ModelBuilder modelBuilder)
///     {
///         modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
///         modelBuilder.ApplyYafModel(this);
///     }
/// }
/// </code>
/// </example>
public abstract class YafDbContext(DbContextOptions options, IYafDbContextServices services) : DbContext(options)
{
    /// <summary>Gets the services the context was created with.</summary>
    protected IYafDbContextServices Services { get; } = services ?? throw new ArgumentNullException(nameof(services));
}

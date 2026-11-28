using Microsoft.EntityFrameworkCore;
using YAF.Persistence.EfCore.Conventions;

namespace YAF.Persistence.EfCore;

/// <summary>Applies YAF's part of the model of a <see cref="YafDbContext"/>.</summary>
public static class YafModelBuilderExtensions
{
    /// <summary>
    /// Applies YAF's model to <paramref name="modelBuilder"/>. Call it last in <see cref="DbContext.OnModelCreating"/>, after the
    /// service's own configuration.
    /// </summary>
    /// <param name="modelBuilder">The builder of the context's model.</param>
    /// <param name="context">The context whose model is built.</param>
    /// <remarks>
    /// It maps every <c>Money</c>, <c>Period</c> and <c>DateRange</c> member of the model's entity types as a complex property, one
    /// column per component (<c>Price_Amount</c>, <c>Price_Currency</c>); a nullable member gets nullable columns. Without this call the
    /// model fails to build with an <see cref="InvalidOperationException"/>.
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// protected override void OnModelCreating(ModelBuilder modelBuilder)
    /// {
    ///     modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    ///     modelBuilder.ApplyYafModel(this);
    /// }
    /// </code>
    /// </example>
    public static void ApplyYafModel(this ModelBuilder modelBuilder, YafDbContext context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(context);
        TypedMemberConventions.MapComplexTypes(modelBuilder);
        modelBuilder.HasAnnotation(YafModelCheckConvention.AppliedAnnotation, true);
    }
}

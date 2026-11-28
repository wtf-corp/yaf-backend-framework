using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace YAF.Persistence.EfCore.Conventions;

/// <summary>
/// Fails the model of a YAF context when <c>ApplyYafModel</c> was not called or a <see langword="string"/> property has no maximum length
/// (05 §2). It runs last among the finalizing conventions, so the provider's own lengths are already set.
/// </summary>
internal sealed class YafModelCheckConvention : IModelFinalizingConvention
{
    /// <summary>The model annotation <c>ApplyYafModel</c> records; the check removes it, so it never reaches a migration.</summary>
    internal const string AppliedAnnotation = "Yaf:ModelApplied";

    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        var model = modelBuilder.Metadata;
        if (model.FindAnnotation(AppliedAnnotation) is null)
        {
            throw new InvalidOperationException(
                "The model does not apply YAF's model: call modelBuilder.ApplyYafModel(this) last in OnModelCreating.");
        }

        model.RemoveAnnotation(AppliedAnnotation);
        foreach (var entityType in model.GetEntityTypes())
        {
            foreach (var property in entityType.GetDeclaredProperties())
            {
                if (property.ClrType == typeof(string) && property.GetMaxLength() is null)
                {
                    throw new InvalidOperationException(
                        $"The string property '{entityType.DisplayName()}.{property.Name}' has no maximum length: set one with HasMaxLength.");
                }
            }
        }
    }
}

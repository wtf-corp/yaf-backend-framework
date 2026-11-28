using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using YAF.Domain.Mementos;

namespace YAF.Persistence.EfCore.Tenancy;

/// <summary>
/// Enforces the tenant of the tenant-owned rows of a save (<c>05</c> §2): an added row without a tenant gets the execution context's tenant;
/// an added or modified row whose tenant, or whose stored tenant, is another one, and a tenant-owned row saved with no tenant in the
/// context, throw <see cref="InvalidOperationException"/>. The message names the entity type, never an id.
/// </summary>
internal sealed class TenantInterceptor(IYafDbContextServices services) : SaveChangesInterceptor
{
    private const string TenantIdName = nameof(ITenantOwnedMemento.TenantId);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Enforce(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Enforce(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Enforce(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ITenantOwnedMemento>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                Enforce(entry);
            }
        }
    }

    private void Enforce(EntityEntry<ITenantOwnedMemento> entry)
    {
        var tenant = services.ExecutionContext.Current.TenantId?.Value
            ?? throw new InvalidOperationException(
                $"A row of {entry.Metadata.DisplayName()} cannot be saved: the execution context has no tenant.");
        var property = entry.Property(TenantIdName);
        if (entry.State == EntityState.Added && (Guid)property.CurrentValue! == Guid.Empty)
        {
            property.CurrentValue = tenant;
        }

        if ((Guid)property.CurrentValue! != tenant || (entry.State == EntityState.Modified && (Guid)property.OriginalValue! != tenant))
        {
            throw new InvalidOperationException(
                $"A row of {entry.Metadata.DisplayName()} belongs to another tenant than the execution context; the save is refused.");
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using YAF.Application.Context;
using YAF.Domain.Mementos;

namespace YAF.Persistence.EfCore.Interceptors;

/// <summary>
/// Stamps the rows of a save from the clock and the execution context: an added row gets its created and modified stamps, a modified row
/// its modified stamps. The stamps record the initiator when there is one, else the acting user, else <see cref="Guid.Empty"/> for a
/// system, service or anonymous actor (<c>04</c> §7). A save without an execution context throws <see cref="MissingExecutionContextException"/>.
/// </summary>
internal sealed class StampingInterceptor(IYafDbContextServices services) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Stamp(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Stamp(DbContext? context)
    {
        var current = services.ExecutionContext.Current;
        if (context is null)
        {
            return;
        }

        var by = current.Initiator?.Id.Value ?? current.Actor.UserId?.Value ?? Guid.Empty;
        var at = services.TimeProvider.GetUtcNow();
        foreach (var entry in context.ChangeTracker.Entries<IStampedMemento>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(IStampedMemento.CreatedAt)).CurrentValue = at;
                entry.Property(nameof(IStampedMemento.CreatedBy)).CurrentValue = by;
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Property(nameof(IStampedMemento.ModifiedAt)).CurrentValue = at;
                entry.Property(nameof(IStampedMemento.ModifiedBy)).CurrentValue = by;
            }
        }
    }
}

using YAF.Application.Context;

namespace YAF.Persistence.EfCore;

/// <summary>
/// What a <see cref="YafDbContext"/> needs from the application's services, bundled so that a derived context has a two-parameter
/// constructor.
/// </summary>
/// <remarks><see cref="YafDbContextServiceCollectionExtensions.AddYafDbContext{TContext}"/> registers it per scope.</remarks>
public interface IYafDbContextServices
{
    /// <summary>Gets the execution context of the current scope: the tenant and the actor of the work the context saves.</summary>
    IExecutionContextAccessor ExecutionContext { get; }

    /// <summary>Gets the clock the context reads the time from.</summary>
    TimeProvider TimeProvider { get; }
}

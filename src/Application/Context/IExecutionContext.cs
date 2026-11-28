using YAF.Core.Ids;

namespace YAF.Application.Context;

/// <summary>
/// Who acts, for which tenant and in which flow of work: the one immutable context of a scope. An entry point of the framework establishes it
/// once per scope; application code reads it through <see cref="IExecutionContextAccessor"/> and can neither create nor replace it.
/// </summary>
public interface IExecutionContext
{
    /// <summary>Gets the tenant the scope works for, or <see langword="null"/> when the work belongs to no tenant.</summary>
    TenantId? TenantId { get; }

    /// <summary>Gets the principal that is acting, and on which authorization, row-level security and rate limits are decided.</summary>
    Principal Actor { get; }

    /// <summary>Gets the user who started the operation, for asynchronous work. It is recorded for audit and grants nothing.</summary>
    PrincipalRef? Initiator { get; }

    /// <summary>Gets the id that relates the logs and traces of this flow of work.</summary>
    CorrelationId CorrelationId { get; }

    /// <summary>Gets the W3C trace parent of the activity that was current when the scope began, if there was one.</summary>
    string? TraceParent { get; }

    /// <summary>Gets the tenants whose data the scope may also read, when a widened read scope was granted; otherwise empty.</summary>
    IReadOnlySet<TenantId> AdditionalTenants { get; }

    /// <summary>Gets the idempotency key the caller sent, if any.</summary>
    string? IdempotencyKey { get; }

    /// <summary>
    /// Gets the kind of entry point that began the scope: <c>http</c>, <c>job</c>, <c>integration-event</c>, <c>cron</c>, <c>process</c> or
    /// <c>test</c>.
    /// </summary>
    string Origin { get; }
}

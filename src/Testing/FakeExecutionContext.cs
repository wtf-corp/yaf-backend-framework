using YAF.Application.Context;
using YAF.Core.Ids;

namespace YAF.Testing;

/// <summary>
/// An execution context for application tests, which is also its own accessor: pass it where a handler or middleware takes an
/// <see cref="IExecutionContext"/> or an <see cref="IExecutionContextAccessor"/>. Its origin is <c>test</c>.
/// </summary>
/// <remarks>The context is immutable; <see cref="WithCorrelation"/> returns a copy.</remarks>
/// <example>
/// <code>
/// var context = FakeExecutionContext.ForUser(UserId.New(), TenantId.New());
/// var handler = new CreateSessionHandler(repository, context);
/// </code>
/// </example>
public sealed class FakeExecutionContext : IExecutionContext, IExecutionContextAccessor
{
    private static readonly IReadOnlySet<TenantId> s_noTenants = new HashSet<TenantId>().AsReadOnly();

    private FakeExecutionContext(TenantId? tenantId, Principal actor, PrincipalRef? initiator, CorrelationId correlationId)
    {
        TenantId = tenantId;
        Actor = actor;
        Initiator = initiator;
        CorrelationId = correlationId;
    }

    /// <inheritdoc/>
    public TenantId? TenantId { get; }

    /// <inheritdoc/>
    public Principal Actor { get; }

    /// <inheritdoc/>
    public PrincipalRef? Initiator { get; }

    /// <inheritdoc/>
    public CorrelationId CorrelationId { get; }

    /// <summary>Gets <see langword="null"/>: a fake context has no trace parent.</summary>
    public string? TraceParent => null;

    /// <summary>Gets an empty set: a fake context grants no widened read scope.</summary>
    public IReadOnlySet<TenantId> AdditionalTenants => s_noTenants;

    /// <summary>Gets <see langword="null"/>: a fake context carries no idempotency key.</summary>
    public string? IdempotencyKey => null;

    /// <summary>Gets <c>test</c>.</summary>
    public string Origin => "test";

    /// <summary>Gets this context.</summary>
    public IExecutionContext Current => this;

    /// <summary>Creates the context of a user who acts in a tenant, with a new correlation id.</summary>
    /// <param name="user">The user who acts.</param>
    /// <param name="tenant">The tenant the user acts in.</param>
    /// <returns>The context, with no initiator.</returns>
    public static FakeExecutionContext ForUser(UserId user, TenantId tenant) =>
        new(tenant, Principal.User(user), null, CorrelationId.New());

    /// <summary>Creates the context of the service acting by itself in a tenant for a reason, such as a job, with a new correlation id.</summary>
    /// <param name="reason">Why the service acts, such as <c>job:send-reminders</c>; required.</param>
    /// <param name="tenant">The tenant the service acts in.</param>
    /// <param name="initiator">The user who started the operation, if any; recorded as the initiator.</param>
    /// <returns>The context, with a system actor.</returns>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is null, empty or white space.</exception>
    public static FakeExecutionContext ForSystem(string reason, TenantId tenant, UserId? initiator = null) =>
        new(tenant, Principal.System(reason), initiator is { } id ? new PrincipalRef(PrincipalKind.User, id) : null, CorrelationId.New());

    /// <summary>Returns a copy of this context with another correlation id.</summary>
    /// <param name="id">The correlation id of the copy.</param>
    /// <returns>The copy; this context is unchanged.</returns>
    public FakeExecutionContext WithCorrelation(CorrelationId id) => new(TenantId, Actor, Initiator, id);
}

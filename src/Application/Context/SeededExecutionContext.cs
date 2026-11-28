using System.Diagnostics;
using YAF.Core.Ids;

namespace YAF.Application.Context;

/// <summary>The execution context an entry point established from its seed, with the trace parent of the activity current at that time.</summary>
internal sealed class SeededExecutionContext(ExecutionContextSeed seed) : IExecutionContext
{
    private static readonly IReadOnlySet<TenantId> s_noTenants = new HashSet<TenantId>().AsReadOnly();

    public TenantId? TenantId => seed.TenantId;

    public Principal Actor => seed.Actor;

    public PrincipalRef? Initiator => seed.Initiator;

    public CorrelationId CorrelationId => seed.CorrelationId;

    public string? TraceParent { get; } = Activity.Current?.Id;

    public IReadOnlySet<TenantId> AdditionalTenants => s_noTenants;

    public string? IdempotencyKey => null;

    public string Origin => seed.Origin;
}

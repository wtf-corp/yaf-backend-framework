using System.Diagnostics;
using YAF.Core.Ids;

namespace YAF.Application.Context;

/// <summary>The execution context an entry point established from its seed.</summary>
internal sealed class SeededExecutionContext : IExecutionContext
{
    private static readonly IReadOnlySet<TenantId> s_noTenants = new HashSet<TenantId>().AsReadOnly();

    public SeededExecutionContext(ExecutionContextSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(seed.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(seed.Origin);
        TenantId = seed.TenantId;
        Actor = seed.Actor;
        Initiator = seed.Initiator;
        CorrelationId = seed.CorrelationId;
        Origin = seed.Origin;
        TraceParent = Activity.Current?.Id;
    }

    public TenantId? TenantId { get; }

    public Principal Actor { get; }

    public PrincipalRef? Initiator { get; }

    public CorrelationId CorrelationId { get; }

    public string? TraceParent { get; }

    public IReadOnlySet<TenantId> AdditionalTenants => s_noTenants;

    public string? IdempotencyKey => null;

    public string Origin { get; }
}

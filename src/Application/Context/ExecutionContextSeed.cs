using YAF.Core.Ids;

namespace YAF.Application.Context;

/// <summary>What an entry point knows when it begins a scope, from which the scope's <see cref="IExecutionContext"/> is made.</summary>
/// <param name="TenantId">The tenant the scope works for, if any.</param>
/// <param name="Actor">The principal that acts.</param>
/// <param name="Initiator">The user who started the operation, for asynchronous work; audit only.</param>
/// <param name="CorrelationId">The id of the flow of work.</param>
/// <param name="Origin">The kind of entry point, such as <c>http</c> or <c>job</c>.</param>
public sealed record ExecutionContextSeed(TenantId? TenantId, Principal Actor, PrincipalRef? Initiator, CorrelationId CorrelationId, string Origin);

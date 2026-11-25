namespace YAF.Core.Ids;

/// <summary>Identifies one flow of work across requests, messages and services, so that its logs and traces can be related.</summary>
[TypedId]
public readonly partial record struct CorrelationId;

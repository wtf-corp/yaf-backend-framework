namespace YAF.Application.Pipeline;

/// <summary>One middleware of the pipeline and the requests it applies to.</summary>
/// <param name="Middleware">The middleware type: an open generic type, or a closed type for one request.</param>
/// <param name="AppliesTo">The requests the middleware runs for.</param>
public sealed record PipelineStep(Type Middleware, PipelineScope AppliesTo);

namespace YAF.Application.Pipeline;

/// <summary>
/// The order of the pipeline as data. A test in every service snapshots <see cref="Render"/> in <c>pipeline.approved.txt</c>, so that an
/// accidental reorder is visible in review.
/// </summary>
public interface IPipelineDescriptor
{
    /// <summary>Gets the steps, outermost first.</summary>
    IReadOnlyList<PipelineStep> Steps { get; }

    /// <summary>Writes the steps one line per step, outermost first: position, middleware name without the generic arity, scope.</summary>
    /// <returns>The text, each line ending in <c>\n</c>, for example <c>1. ValidationMiddleware (all)</c>.</returns>
    string Render();
}

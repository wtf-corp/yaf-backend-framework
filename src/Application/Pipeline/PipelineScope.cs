namespace YAF.Application.Pipeline;

/// <summary>The requests a <see cref="PipelineStep"/> applies to.</summary>
public enum PipelineScope
{
    /// <summary>Every request.</summary>
    All,

    /// <summary>Requests that implement <see cref="ICommand{TResult}"/>.</summary>
    Commands,

    /// <summary>Requests that implement <see cref="IQuery{TResult}"/>.</summary>
    Queries,
}

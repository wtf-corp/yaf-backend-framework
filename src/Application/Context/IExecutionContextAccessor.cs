namespace YAF.Application.Context;

/// <summary>Gives the execution context of the current scope.</summary>
public interface IExecutionContextAccessor
{
    /// <summary>Gets the execution context of the current scope.</summary>
    /// <exception cref="MissingExecutionContextException">No entry point has established a context for the scope.</exception>
    IExecutionContext Current { get; }
}

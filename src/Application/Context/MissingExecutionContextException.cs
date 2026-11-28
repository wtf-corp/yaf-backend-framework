namespace YAF.Application.Context;

/// <summary>Thrown when code reads the execution context of a scope that no entry point has established one for.</summary>
public sealed class MissingExecutionContextException : InvalidOperationException
{
    /// <summary>Creates the exception with a default message.</summary>
    public MissingExecutionContextException()
        : base("No execution context was established for this scope.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public MissingExecutionContextException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the exception that caused it.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The exception that caused it.</param>
    public MissingExecutionContextException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

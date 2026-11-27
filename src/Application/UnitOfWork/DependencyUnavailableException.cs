namespace YAF.Application.UnitOfWork;

/// <summary>
/// Thrown by an adapter when a store, broker or other service the request needs cannot be reached. The exception boundary of the pipeline
/// turns it into a failure with <see cref="ApplicationErrors.DependencyUnavailable"/>.
/// </summary>
public sealed class DependencyUnavailableException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public DependencyUnavailableException()
        : base("A service this request needs is not available.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message; it must not contain the values of the request or of the stored data.</param>
    public DependencyUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the exception that caused it.</summary>
    /// <param name="message">The message; it must not contain the values of the request or of the stored data.</param>
    /// <param name="innerException">The adapter's own exception.</param>
    public DependencyUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

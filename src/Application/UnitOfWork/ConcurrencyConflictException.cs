namespace YAF.Application.UnitOfWork;

/// <summary>
/// Thrown by the store when an aggregate was changed by someone else since it was read. The exception boundary of the pipeline turns it into
/// a failure with <see cref="ApplicationErrors.ConcurrencyConflict"/>.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public ConcurrencyConflictException()
        : base("The resource was changed by someone else.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message; it must not contain the values of the request or of the stored data.</param>
    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the exception that caused it.</summary>
    /// <param name="message">The message; it must not contain the values of the request or of the stored data.</param>
    /// <param name="innerException">The store's own exception.</param>
    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

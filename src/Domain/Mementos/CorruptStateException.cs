namespace YAF.Domain.Mementos;

/// <summary>
/// The exception thrown when stored state cannot be restored, for example a memento whose id is not the entity's. It signals a bug or
/// tampering, never a business outcome.
/// </summary>
/// <remarks>
/// Restoring never validates: stored state is trusted. A message names types and ids, never the values of members, which may be personal
/// data.
/// </remarks>
public sealed class CorruptStateException : Exception
{
    /// <summary>Initializes a new instance with a default message.</summary>
    public CorruptStateException()
    {
    }

    /// <summary>Initializes a new instance with <paramref name="message"/>.</summary>
    /// <param name="message">What could not be restored, with type names and ids and no values.</param>
    public CorruptStateException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with <paramref name="message"/> and the exception that caused it.</summary>
    /// <param name="message">What could not be restored, with type names and ids and no values.</param>
    /// <param name="innerException">The exception that caused it.</param>
    public CorruptStateException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

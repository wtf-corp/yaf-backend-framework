using System.Collections.ObjectModel;

namespace YAF.Core.Results;

/// <summary>
/// An error carried by a failed result: the code and kind of its <see cref="ErrorDescriptor"/>, the filled English message, and optionally
/// the property path it is about and the template arguments.
/// </summary>
/// <remarks>
/// Do not create an error inline. Declare an <see cref="ErrorDescriptor"/> in an <see cref="IErrorCatalog"/> with one of the static
/// factories of this type, and create the error from it with <see cref="ErrorDescriptor.ToError"/>,
/// <see cref="ErrorDescriptor.With(ReadOnlySpan{ValueTuple{string, object}})"/> or <see cref="ErrorDescriptor.At(string)"/>.
/// The factories need the error code generator shipped in the <c>YAF.Core</c> package and the MSBuild property <c>YafServiceCode</c>:
/// the generator replaces each call with a descriptor that carries the generated code. Without it a factory throws
/// <see cref="InvalidOperationException"/>; called from a static field initialiser, at type initialisation. There is no fallback code.
/// </remarks>
/// <param name="Code">The error code, lower-case <c>[a-z0-9_.]</c>, for example <c>catalog.session.full</c>. Clients match on it.</param>
/// <param name="Kind">The kind of the error, which decides the HTTP status.</param>
/// <param name="Message">The English message, safe to show to an end user.</param>
public sealed record Error(string Code, ErrorKind Kind, string Message)
{
    private const string NoGeneratorMessage =
        "The error code generator did not replace this call, so the error has no code. Reference the package YAF.Core directly and set the "
        + "MSBuild property YafServiceCode, for example <YafServiceCode>catalog</YafServiceCode>, in the project that declares the error.";

    /// <summary>Gets the property path the error is about, for example <c>speakers[1].email</c>, or <see langword="null"/> when there is none.</summary>
    public string? Target { get; init; }

    /// <summary>Gets the template arguments, for client-side localisation. Empty when the error was created without arguments.</summary>
    public IReadOnlyDictionary<string, object?> Arguments { get; init; } = ReadOnlyDictionary<string, object?>.Empty;

    /// <summary>Declares an error of kind <see cref="ErrorKind.Validation"/>. Call it only in a static field initialiser of an <see cref="IErrorCatalog"/>.</summary>
    /// <param name="messageTemplate">The English message, with optional <c>{Name}</c> placeholders.</param>
    /// <returns>The descriptor with the generated code.</returns>
    /// <exception cref="InvalidOperationException">The error code generator did not replace the call.</exception>
    public static ErrorDescriptor Validation(string messageTemplate) => throw new InvalidOperationException(NoGeneratorMessage);

    /// <summary>Declares an error of kind <see cref="ErrorKind.Unauthenticated"/>. Call it only in a static field initialiser of an <see cref="IErrorCatalog"/>.</summary>
    /// <param name="messageTemplate">The English message, with optional <c>{Name}</c> placeholders.</param>
    /// <returns>The descriptor with the generated code.</returns>
    /// <exception cref="InvalidOperationException">The error code generator did not replace the call.</exception>
    public static ErrorDescriptor Unauthenticated(string messageTemplate) => throw new InvalidOperationException(NoGeneratorMessage);

    /// <summary>Declares an error of kind <see cref="ErrorKind.Forbidden"/>. Call it only in a static field initialiser of an <see cref="IErrorCatalog"/>.</summary>
    /// <param name="messageTemplate">The English message, with optional <c>{Name}</c> placeholders.</param>
    /// <returns>The descriptor with the generated code.</returns>
    /// <exception cref="InvalidOperationException">The error code generator did not replace the call.</exception>
    public static ErrorDescriptor Forbidden(string messageTemplate) => throw new InvalidOperationException(NoGeneratorMessage);

    /// <summary>Declares an error of kind <see cref="ErrorKind.NotFound"/>. Call it only in a static field initialiser of an <see cref="IErrorCatalog"/>.</summary>
    /// <param name="messageTemplate">The English message, with optional <c>{Name}</c> placeholders.</param>
    /// <returns>The descriptor with the generated code.</returns>
    /// <exception cref="InvalidOperationException">The error code generator did not replace the call.</exception>
    public static ErrorDescriptor NotFound(string messageTemplate) => throw new InvalidOperationException(NoGeneratorMessage);

    /// <summary>Declares an error of kind <see cref="ErrorKind.Conflict"/>. Call it only in a static field initialiser of an <see cref="IErrorCatalog"/>.</summary>
    /// <param name="messageTemplate">The English message, with optional <c>{Name}</c> placeholders.</param>
    /// <returns>The descriptor with the generated code.</returns>
    /// <exception cref="InvalidOperationException">The error code generator did not replace the call.</exception>
    public static ErrorDescriptor Conflict(string messageTemplate) => throw new InvalidOperationException(NoGeneratorMessage);

    /// <summary>Declares an error of kind <see cref="ErrorKind.PreconditionFailed"/>. Call it only in a static field initialiser of an <see cref="IErrorCatalog"/>.</summary>
    /// <param name="messageTemplate">The English message, with optional <c>{Name}</c> placeholders.</param>
    /// <returns>The descriptor with the generated code.</returns>
    /// <exception cref="InvalidOperationException">The error code generator did not replace the call.</exception>
    public static ErrorDescriptor PreconditionFailed(string messageTemplate) => throw new InvalidOperationException(NoGeneratorMessage);

    /// <summary>Declares an error of kind <see cref="ErrorKind.RateLimited"/>. Call it only in a static field initialiser of an <see cref="IErrorCatalog"/>.</summary>
    /// <param name="messageTemplate">The English message, with optional <c>{Name}</c> placeholders.</param>
    /// <returns>The descriptor with the generated code.</returns>
    /// <exception cref="InvalidOperationException">The error code generator did not replace the call.</exception>
    public static ErrorDescriptor RateLimited(string messageTemplate) => throw new InvalidOperationException(NoGeneratorMessage);

    /// <summary>Declares an error of kind <see cref="ErrorKind.Unavailable"/>. Call it only in a static field initialiser of an <see cref="IErrorCatalog"/>.</summary>
    /// <param name="messageTemplate">The English message, with optional <c>{Name}</c> placeholders.</param>
    /// <returns>The descriptor with the generated code.</returns>
    /// <exception cref="InvalidOperationException">The error code generator did not replace the call.</exception>
    public static ErrorDescriptor Unavailable(string messageTemplate) => throw new InvalidOperationException(NoGeneratorMessage);

    /// <summary>Declares an error of kind <see cref="ErrorKind.Failure"/>. Call it only in a static field initialiser of an <see cref="IErrorCatalog"/>.</summary>
    /// <param name="messageTemplate">The English message, with optional <c>{Name}</c> placeholders.</param>
    /// <returns>The descriptor with the generated code.</returns>
    /// <exception cref="InvalidOperationException">The error code generator did not replace the call.</exception>
    public static ErrorDescriptor Failure(string messageTemplate) => throw new InvalidOperationException(NoGeneratorMessage);
}

using YAF.Core.Results;

namespace YAF.Domain;

/// <summary>The errors that <c>YAF.Domain</c> itself reports.</summary>
public sealed class DomainErrors : IErrorCatalog
{
    /// <summary>
    /// The error of <see cref="Enumeration{TSelf}.FromCode"/> for a code that no instance has: code <c>core.enumeration.unknown_code</c>,
    /// kind <see cref="ErrorKind.Validation"/>. Its arguments are <c>Code</c>, the code given, and <c>Enumeration</c>, the name of the type.
    /// </summary>
    [ErrorCode("core.enumeration.unknown_code")]
    public static readonly ErrorDescriptor UnknownEnumerationCode = Error.Validation("'{Code}' is not a code of {Enumeration}.");

    private DomainErrors()
    {
    }
}

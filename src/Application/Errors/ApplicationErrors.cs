using YAF.Core.Results;

namespace YAF.Application.UnitOfWork;

/// <summary>The errors that <c>YAF.Application</c> itself reports.</summary>
public sealed class ApplicationErrors : IErrorCatalog
{
    /// <summary>
    /// The error of a request whose aggregate was changed by someone else since it was read (<see cref="ConcurrencyConflictException"/>):
    /// code <c>core.concurrency.conflict</c>, kind <see cref="ErrorKind.Conflict"/>.
    /// </summary>
    [ErrorCode("core.concurrency.conflict")]
    public static readonly ErrorDescriptor ConcurrencyConflict = Error.Conflict("The resource was changed by someone else. Reload it and try again.");

    /// <summary>
    /// The error of a request that needs a service that cannot be reached (<see cref="DependencyUnavailableException"/>): code
    /// <c>core.dependency.unavailable</c>, kind <see cref="ErrorKind.Unavailable"/>.
    /// </summary>
    [ErrorCode("core.dependency.unavailable")]
    public static readonly ErrorDescriptor DependencyUnavailable = Error.Unavailable("A service this request needs is not available. Try again later.");

    private ApplicationErrors()
    {
    }
}

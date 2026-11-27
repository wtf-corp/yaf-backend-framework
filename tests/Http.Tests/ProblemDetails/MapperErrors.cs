using YAF.Core.Results;

namespace Http.Tests.ProblemDetails;

internal abstract class MapperErrors : IErrorCatalog
{
    public static readonly ErrorDescriptor TitleRequired = Error.Validation("A session needs a title.");

    public static readonly ErrorDescriptor NotSignedIn = Error.Unauthenticated("Sign in to continue.");

    public static readonly ErrorDescriptor NotAllowed = Error.Forbidden("You may not change this session.");

    public static readonly ErrorDescriptor Missing = Error.NotFound("The session was not found.");

    public static readonly ErrorDescriptor Full = Error.Conflict("Session has no free seats (capacity {Capacity}).");

    public static readonly ErrorDescriptor VersionMismatch = Error.PreconditionFailed("The session was changed since you read it.");

    public static readonly ErrorDescriptor TooManyRequests = Error.RateLimited("Too many requests. Try again later.");

    public static readonly ErrorDescriptor StoreDown = Error.Unavailable("The session store is not available.");

    public static readonly ErrorDescriptor Internal = Error.Failure("Connection to db-01.internal refused for user {User}.");

    public static ErrorDescriptor OfKind(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => TitleRequired,
        ErrorKind.Unauthenticated => NotSignedIn,
        ErrorKind.Forbidden => NotAllowed,
        ErrorKind.NotFound => Missing,
        ErrorKind.Conflict => Full,
        ErrorKind.PreconditionFailed => VersionMismatch,
        ErrorKind.RateLimited => TooManyRequests,
        ErrorKind.Unavailable => StoreDown,
        ErrorKind.Failure => Internal,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}

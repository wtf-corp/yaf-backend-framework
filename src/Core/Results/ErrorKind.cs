namespace YAF.Core.Results;

/// <summary>
/// The kind of an error. It is chosen when the error is declared, never where it is returned, so one error always maps to one HTTP status.
/// </summary>
public enum ErrorKind
{
    /// <summary>The input or the requested change breaks a rule (HTTP 400, or 422 when configured).</summary>
    Validation = 1,

    /// <summary>There is no valid identity (HTTP 401).</summary>
    Unauthenticated,

    /// <summary>The identity lacks the permission (HTTP 403).</summary>
    Forbidden,

    /// <summary>The resource does not exist, or is not visible to the caller (HTTP 404).</summary>
    NotFound,

    /// <summary>A state conflict, such as a duplicate or a stale stored version (HTTP 409).</summary>
    Conflict,

    /// <summary>The expected version the client supplied (<c>If-Match</c>) does not match (HTTP 412).</summary>
    PreconditionFailed,

    /// <summary>The caller sent too many requests (HTTP 429).</summary>
    RateLimited,

    /// <summary>A dependency is down; a retry is safe (HTTP 503).</summary>
    Unavailable,

    /// <summary>Any other expected outcome (HTTP 500 with a generic detail).</summary>
    Failure,
}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAF.Core.Results;

namespace YAF.Http;

/// <summary>
/// Turns the errors of a failed result into an RFC 9457 problem document. Every endpoint style answers a failure through it, so a
/// failure has the same status and body whichever style serves it.
/// </summary>
/// <remarks>
/// The status follows the kinds: when every error is <see cref="ErrorKind.Validation"/> it is <see cref="HttpOptions.ValidationStatusCode"/>;
/// otherwise the most severe kind decides, in the order <see cref="ErrorKind.Unauthenticated"/>, <see cref="ErrorKind.Forbidden"/>,
/// <see cref="ErrorKind.RateLimited"/>, <see cref="ErrorKind.Unavailable"/>, <see cref="ErrorKind.Failure"/>,
/// <see cref="ErrorKind.PreconditionFailed"/>, <see cref="ErrorKind.Conflict"/>, <see cref="ErrorKind.NotFound"/>,
/// <see cref="ErrorKind.Validation"/>. The first error of that kind gives <c>type</c> (<see cref="ProblemDetailsOptions.TypeBaseUri"/>
/// followed by its code), <c>detail</c> and the extension <c>code</c>. The extensions also hold <c>traceId</c> and <c>errors</c>, an array
/// with the <c>code</c>, <c>message</c>, <c>target</c> and <c>arguments</c> of every error. A <see cref="ErrorKind.Failure"/> error shows
/// the generic "An unexpected error occurred." and no arguments. The mapper does not rewrite kinds. Register it with
/// <see cref="YafHttpExtensions.AddYafHttp(Microsoft.Extensions.Hosting.IHostApplicationBuilder)"/>.
/// </remarks>
public interface IProblemDetailsMapper
{
    /// <summary>Builds the problem document for <paramref name="errors"/>.</summary>
    /// <param name="errors">The errors of the failed result, at least one.</param>
    /// <param name="httpContext">The request being answered; its path becomes <c>instance</c>.</param>
    /// <returns>The problem document, with its status set.</returns>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty.</exception>
    ProblemDetails Map(IReadOnlyList<Error> errors, HttpContext httpContext);
}

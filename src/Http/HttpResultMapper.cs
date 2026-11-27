using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using YAF.Core.Results;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

namespace YAF.Http;

/// <summary>
/// Turns the <see cref="Result"/> or <see cref="Result{T}"/> of a use case into an HTTP response. Every endpoint style answers through
/// it, so a result gives the same status and body whichever style serves it.
/// </summary>
/// <remarks>
/// A success is <c>200</c> with the value as the body, <c>204</c> for a <see cref="Result"/>, or <c>201</c> or <c>202</c> with a
/// <c>Location</c> header and the value as the body through <see cref="ToCreatedResponse{T}"/> and <see cref="ToAcceptedResponse{T}"/>.
/// A failure, including a <see langword="default"/> result, is the problem document of the <see cref="IProblemDetailsMapper"/> that
/// <see cref="YafHttpExtensions.AddYafHttp(Microsoft.Extensions.Hosting.IHostApplicationBuilder)"/> registers, with its status.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var result = await dispatcher.SendAsync(new CreateSessionCommand(title), cancellationToken);
/// return HttpResultMapper.ToCreatedResponse(result, session => new Uri($"/api/v1/sessions/{session.Id}", UriKind.Relative), httpContext);
/// </code>
/// </example>
public static class HttpResultMapper
{
    /// <summary>Answers a result without a value: <c>204 No Content</c> on success, a problem document on failure.</summary>
    /// <param name="result">The result of the use case.</param>
    /// <param name="httpContext">The request being answered; its services must hold the <see cref="IProblemDetailsMapper"/>.</param>
    /// <returns>The response to execute.</returns>
    /// <exception cref="InvalidOperationException">The result is a failure and no <see cref="IProblemDetailsMapper"/> is registered.</exception>
    public static HttpResult ToResponse(Result result, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return result.IsSuccess ? TypedResults.NoContent() : Problem(result.Errors, httpContext);
    }

    /// <summary>Answers a result with a value: <c>200 OK</c> with the value as the body on success, a problem document on failure.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result of the use case.</param>
    /// <param name="httpContext">The request being answered; its services must hold the <see cref="IProblemDetailsMapper"/>.</param>
    /// <returns>The response to execute.</returns>
    /// <exception cref="InvalidOperationException">The result is a failure and no <see cref="IProblemDetailsMapper"/> is registered.</exception>
    public static HttpResult ToResponse<T>(Result<T> result, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : Problem(result.Errors, httpContext);
    }

    /// <summary>
    /// Answers the creation of a resource: <c>201 Created</c> with a <c>Location</c> header and the value as the body on success, a
    /// problem document on failure.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result of the use case.</param>
    /// <param name="location">Gives the URI of the created resource from the value; called only on success.</param>
    /// <param name="httpContext">The request being answered; its services must hold the <see cref="IProblemDetailsMapper"/>.</param>
    /// <returns>The response to execute.</returns>
    /// <exception cref="InvalidOperationException">The result is a failure and no <see cref="IProblemDetailsMapper"/> is registered.</exception>
    public static HttpResult ToCreatedResponse<T>(Result<T> result, Func<T, Uri> location, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(httpContext);
        return result.IsSuccess ? TypedResults.Created(location(result.Value), result.Value) : Problem(result.Errors, httpContext);
    }

    /// <summary>
    /// Answers work accepted to run later: <c>202 Accepted</c> with a <c>Location</c> header and the value as the body on success, a
    /// problem document on failure.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result of the use case.</param>
    /// <param name="location">Gives the URI where the caller follows the work, from the value; called only on success.</param>
    /// <param name="httpContext">The request being answered; its services must hold the <see cref="IProblemDetailsMapper"/>.</param>
    /// <returns>The response to execute.</returns>
    /// <exception cref="InvalidOperationException">The result is a failure and no <see cref="IProblemDetailsMapper"/> is registered.</exception>
    public static HttpResult ToAcceptedResponse<T>(Result<T> result, Func<T, Uri> location, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(httpContext);
        return result.IsSuccess ? TypedResults.Accepted(location(result.Value), result.Value) : Problem(result.Errors, httpContext);
    }

    private static ProblemHttpResult Problem(IReadOnlyList<Error> errors, HttpContext httpContext) =>
        TypedResults.Problem(httpContext.RequestServices.GetRequiredService<IProblemDetailsMapper>().Map(errors, httpContext));
}

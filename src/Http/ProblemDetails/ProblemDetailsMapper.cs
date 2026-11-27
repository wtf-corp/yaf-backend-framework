using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using YAF.Core.Results;

namespace YAF.Http;

/// <summary>The mapping of <c>03</c> §4: status from the most severe kind, <c>type</c> from the base URI and the code.</summary>
internal sealed class ProblemDetailsMapper(IOptions<HttpOptions> httpOptions, IOptions<ProblemDetailsOptions> problemDetailsOptions)
    : IProblemDetailsMapper
{
    private const string GenericDetail = "An unexpected error occurred.";

    private readonly int _validationStatusCode = httpOptions.Value.ValidationStatusCode;

    private readonly string _typeBaseUri = problemDetailsOptions.Value.TypeBaseUri.AbsoluteUri;

    public ProblemDetails Map(IReadOnlyList<Error> errors, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(httpContext);
        if (errors.Count == 0)
        {
            throw new ArgumentException("A problem document needs at least one error.", nameof(errors));
        }

        var deciding = errors[0];
        foreach (var error in errors)
        {
            if (Severity(error.Kind) < Severity(deciding.Kind))
            {
                deciding = error;
            }
        }

        var status = StatusOf(deciding.Kind);
        var problem = new ProblemDetails
        {
            Type = _typeBaseUri + deciding.Code,
            Title = ReasonPhrases.GetReasonPhrase(status),
            Status = status,
            Detail = Shown(deciding).Message,
            Instance = httpContext.Request.Path.Value,
        };
        problem.Extensions["code"] = deciding.Code;
        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToHexString() ?? httpContext.TraceIdentifier;
        problem.Extensions["errors"] = errors.Select(Shown).ToList();
        return problem;
    }

    // A kind outside the enumeration maps to 500 like Failure, so its message is hidden too (fail closed).
    private static ProblemDetailsError Shown(Error error) => error.Kind == ErrorKind.Failure || !Enum.IsDefined(error.Kind)
        ? new ProblemDetailsError(error.Code, GenericDetail, error.Target, ReadOnlyDictionary<string, object?>.Empty)
        : new ProblemDetailsError(error.Code, error.Message, error.Target, error.Arguments);

    // 03 §4: the lower the number, the more severe the kind.
    private static int Severity(ErrorKind kind) => kind switch
    {
        ErrorKind.Unauthenticated => 0,
        ErrorKind.Forbidden => 1,
        ErrorKind.RateLimited => 2,
        ErrorKind.Unavailable => 3,
        ErrorKind.Failure => 4,
        ErrorKind.PreconditionFailed => 5,
        ErrorKind.Conflict => 6,
        ErrorKind.NotFound => 7,
        ErrorKind.Validation => 8,
        _ => 4,
    };

    private int StatusOf(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => _validationStatusCode,
        ErrorKind.Unauthenticated => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.PreconditionFailed => StatusCodes.Status412PreconditionFailed,
        ErrorKind.RateLimited => StatusCodes.Status429TooManyRequests,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError,
    };
}

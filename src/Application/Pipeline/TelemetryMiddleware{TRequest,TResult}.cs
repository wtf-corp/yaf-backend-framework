using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using YAF.Core.Results;

namespace YAF.Application.Pipeline;

/// <summary>
/// The outermost step of the default pipeline: runs the rest in an activity <c>yaf.request {RequestName}</c> of source
/// <c>YAF.Application</c>, records <c>yaf.request.duration</c> (s; request name, kind, outcome), and logs <c>Handling</c>,
/// <c>Handled</c> and <c>Failed {ErrorCodes}</c> (event ids 1001–1003) with the request's name and error codes only.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResult">The request's result type.</typeparam>
/// <remarks>
/// The outcome is <c>success</c>, the kind of the first error (<c>validation</c>, <c>not_found</c>, …) or <c>exception</c>. The status is
/// <see cref="ActivityStatusCode.Error"/> only for an exception and for a <see cref="ErrorKind.Failure"/> or
/// <see cref="ErrorKind.Unavailable"/> error (<c>12</c> §4). An exception is rethrown unchanged and not logged: the exception boundary
/// inside this step logs it.
/// </remarks>
public sealed class TelemetryMiddleware<TRequest, TResult> : IRequestMiddleware<TRequest, TResult>
    where TRequest : IRequest<TResult>
    where TResult : IResult
{
    private static readonly string s_name = TelemetryMiddleware.RequestName(typeof(TRequest));

    private static readonly string s_kind = typeof(ICommand<TResult>).IsAssignableFrom(typeof(TRequest)) ? "command"
        : typeof(IQuery<TResult>).IsAssignableFrom(typeof(TRequest)) ? "query"
        : "request";

    private readonly ILogger<TelemetryMiddleware<TRequest, TResult>> _logger;
    private readonly Meter _meter;
    private readonly Histogram<double> _duration;

    /// <summary>Creates the middleware; the dispatcher creates it per request from the scope.</summary>
    /// <param name="logger">The logger of the three request events.</param>
    /// <param name="meterFactory">The factory of meter <c>YAF.Application</c>.</param>
    public TelemetryMiddleware(ILogger<TelemetryMiddleware<TRequest, TResult>> logger, IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(meterFactory);
        _logger = logger;
        _meter = TelemetryMiddleware.CreateMeter(meterFactory);
        _duration = _meter.CreateHistogram<double>("yaf.request.duration", "s", "How long requests take in the pipeline.");
    }

    /// <summary>Runs the rest of the pipeline inside the request's activity and records its duration and outcome.</summary>
    /// <param name="request">The request; only its type is recorded.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The result of <paramref name="next"/>, unchanged.</returns>
    public async ValueTask<TResult> InvokeAsync(TRequest request, RequestDelegate<TResult> next, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(next);
        using var activity = TelemetryMiddleware.Source.StartActivity("yaf.request", ActivityKind.Internal);
        activity?.DisplayName = $"yaf.request {s_name}";
        activity?.SetTag("yaf.request.kind", s_kind);
        TelemetryMiddleware.Log.Handling(_logger, s_name);

        var started = Stopwatch.GetTimestamp();
        var outcome = "exception";
        try
        {
            var result = await next(ct).ConfigureAwait(false);
            outcome = Record(activity, result);
            return result;
        }
        catch
        {
            activity?.SetTag("yaf.request.outcome", outcome);
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        finally
        {
            _duration.Record(
                Stopwatch.GetElapsedTime(started).TotalSeconds,
                new("yaf.request.name", s_name),
                new("yaf.request.kind", s_kind),
                new("yaf.request.outcome", outcome));
        }
    }

    private string Record(Activity? activity, TResult result)
    {
        if (result.IsSuccess)
        {
            activity?.SetTag("yaf.request.outcome", "success");
            TelemetryMiddleware.Log.Handled(_logger, s_name);
            return "success";
        }

        var outcome = TelemetryMiddleware.Outcome(result.Errors[0].Kind);
        var codes = string.Join(',', result.Errors.Select(error => error.Code));
        var severe = result.Errors.Any(error => error.Kind is ErrorKind.Failure or ErrorKind.Unavailable);
        activity?.SetTag("yaf.request.outcome", outcome);
        activity?.SetTag("yaf.error.codes", codes);
        if (severe)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
        }

        TelemetryMiddleware.Log.Failed(_logger, severe ? LogLevel.Error : LogLevel.Debug, s_name, codes);
        return outcome;
    }
}

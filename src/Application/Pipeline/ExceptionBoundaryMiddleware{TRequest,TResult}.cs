using Microsoft.Extensions.Logging;
using YAF.Application.UnitOfWork;
using YAF.Core.Results;

namespace YAF.Application.Pipeline;

/// <summary>
/// The second step of the default pipeline: turns the known infrastructure exceptions into failures, lets cancellation pass, and logs any
/// other exception once before rethrowing it.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResult">The request's result type.</typeparam>
/// <remarks>
/// <see cref="ConcurrencyConflictException"/> becomes <see cref="ApplicationErrors.ConcurrencyConflict"/> and
/// <see cref="DependencyUnavailableException"/> becomes <see cref="ApplicationErrors.DependencyUnavailable"/>.
/// <see cref="OperationCanceledException"/> is rethrown and not logged. Any other exception is logged as <c>Unhandled</c> (event id 1004)
/// with its type and the request's name, and rethrown unchanged.
/// </remarks>
public sealed class ExceptionBoundaryMiddleware<TRequest, TResult> : IRequestMiddleware<TRequest, TResult>
    where TRequest : IRequest<TResult>
    where TResult : IResult
{
    private static readonly string s_name = TelemetryMiddleware.RequestName(typeof(TRequest));

    private readonly ILogger<ExceptionBoundaryMiddleware<TRequest, TResult>> _logger;

    /// <summary>Creates the middleware; the dispatcher creates it per request from the scope.</summary>
    /// <param name="logger">The logger of the <c>Unhandled</c> event.</param>
    public ExceptionBoundaryMiddleware(ILogger<ExceptionBoundaryMiddleware<TRequest, TResult>> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>Runs the rest of the pipeline and turns the known infrastructure exceptions into failures.</summary>
    /// <param name="request">The request; only its type is logged.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The result of <paramref name="next"/>, or a failure with the error of a known infrastructure exception.</returns>
    public async ValueTask<TResult> InvokeAsync(TRequest request, RequestDelegate<TResult> next, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(next);
        try
        {
            return await next(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ConcurrencyConflictException)
        {
            return ResultFactory.Failure<TResult>([ApplicationErrors.ConcurrencyConflict.ToError()]);
        }
        catch (DependencyUnavailableException)
        {
            return ResultFactory.Failure<TResult>([ApplicationErrors.DependencyUnavailable.ToError()]);
        }
        catch (Exception exception)
        {
            ExceptionBoundaryMiddleware.Log.Unhandled(_logger, exception, exception.GetType().Name, s_name);
            throw;
        }
    }
}

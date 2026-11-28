using YAF.Application.UnitOfWork;
using YAF.Core.Results;

namespace YAF.Application.Pipeline;

/// <summary>
/// The unit-of-work step of the default pipeline: runs the rest of the pipeline of a command inside <see cref="IUnitOfWork"/>, so that the
/// handler's changes and what its domain events' handlers write commit together or not at all.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResult">The request's result type.</typeparam>
/// <remarks>A request that is not an <see cref="ICommand{TResult}"/> goes straight to the rest of the pipeline.</remarks>
public sealed class UnitOfWorkMiddleware<TRequest, TResult> : IRequestMiddleware<TRequest, TResult>
    where TRequest : IRequest<TResult>
    where TResult : IResult
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Creates the middleware; the dispatcher creates it per request from the scope.</summary>
    /// <param name="unitOfWork">The unit of work of the request's scope.</param>
    public UnitOfWorkMiddleware(IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _unitOfWork = unitOfWork;
    }

    /// <summary>Runs the rest of the pipeline inside the unit of work when the request is a command.</summary>
    /// <param name="request">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The result of the unit of work for a command; the result of <paramref name="next"/> for any other request.</returns>
    public ValueTask<TResult> InvokeAsync(TRequest request, RequestDelegate<TResult> next, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(next);
        return request is ICommand<TResult>
            ? _unitOfWork.ExecuteAsync(token => next(token), ct)
            : next(ct);
    }
}

using YAF.Application.Validation;
using YAF.Core.Results;

namespace YAF.Application.Pipeline;

/// <summary>
/// The validation step of the default pipeline: runs every <see cref="IValidator{T}"/> registered for the request and, when any of them
/// fails, returns their combined errors without running the rest of the pipeline.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResult">The request's result type.</typeparam>
/// <remarks>
/// The validators run one after the other, in their registration order, and all of them run even when an earlier one fails, so the caller
/// sees every broken rule at once. A request with no validator goes straight to the rest of the pipeline.
/// </remarks>
public sealed class ValidationMiddleware<TRequest, TResult> : IRequestMiddleware<TRequest, TResult>
    where TRequest : IRequest<TResult>
    where TResult : IResult
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>Creates the middleware; the dispatcher creates it per request from the scope.</summary>
    /// <param name="validators">The validators registered for <typeparamref name="TRequest"/>; empty when it has none.</param>
    public ValidationMiddleware(IEnumerable<IValidator<TRequest>> validators)
    {
        ArgumentNullException.ThrowIfNull(validators);
        _validators = validators;
    }

    /// <summary>Runs every validator of the request, then the rest of the pipeline when all of them succeed.</summary>
    /// <param name="request">The request to validate.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>A failure with the errors of every failed validator, in order; otherwise the result of <paramref name="next"/>.</returns>
    public async ValueTask<TResult> InvokeAsync(TRequest request, RequestDelegate<TResult> next, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(next);
        List<Error>? errors = null;
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(request, ct).ConfigureAwait(false);
            if (result.IsFailure)
            {
                (errors ??= []).AddRange(result.Errors);
            }
        }

        return errors is null ? await next(ct).ConfigureAwait(false) : ResultFactory.Failure<TResult>(errors);
    }
}

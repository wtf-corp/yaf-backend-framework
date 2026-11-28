using YAF.Core.Results;

namespace YAF.Application.UnitOfWork;

/// <summary>
/// Runs a command's work as one transaction: the work, then rounds of saving the changes and publishing the domain events they raised,
/// then the commit. <see cref="Pipeline.UnitOfWorkMiddleware{TRequest, TResult}"/> runs every command through it.
/// </summary>
/// <remarks>
/// A failed result of the work, or of a domain event handler, rolls everything back and is returned. Domain events that keep raising
/// further events beyond the depth limit fail with <see cref="ApplicationErrors.DomainEventDepthExceeded"/>. An implementation may run
/// <c>work</c> again after a transient store failure, so the work must have no effect outside the transaction.
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>Runs <paramref name="work"/> in a transaction and commits it when the work and its domain events succeed.</summary>
    /// <typeparam name="TResult">The work's result type.</typeparam>
    /// <param name="work">The work, usually the rest of the request pipeline.</param>
    /// <param name="ct">Cancels the work and the transaction.</param>
    /// <returns>The result of the work, or the failure that rolled the transaction back.</returns>
    ValueTask<TResult> ExecuteAsync<TResult>(Func<CancellationToken, ValueTask<TResult>> work, CancellationToken ct)
        where TResult : IResult;
}

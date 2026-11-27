using YAF.Core.Results;

namespace YAF.Application;

/// <summary>A request that only reads. The pipeline runs it in a read-only transaction, so a write fails.</summary>
/// <typeparam name="TResult">The outcome of the query: <see cref="Result"/> or <see cref="Result{T}"/>.</typeparam>
public interface IQuery<TResult> : IRequest<TResult>;

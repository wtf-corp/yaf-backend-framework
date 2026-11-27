using YAF.Core.Results;

namespace YAF.Application;

/// <summary>A request that changes state. The pipeline runs it in a unit of work, which commits only when the handler succeeds.</summary>
/// <typeparam name="TResult">The outcome of the command: <see cref="Result"/> or <see cref="Result{T}"/>.</typeparam>
public interface ICommand<TResult> : IRequest<TResult>;

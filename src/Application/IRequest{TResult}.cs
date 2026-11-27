using YAF.Core.Results;

namespace YAF.Application;

/// <summary>
/// A use case sent through <see cref="IDispatcher.SendAsync{TResult}"/>. Implement <see cref="ICommand{TResult}"/> or
/// <see cref="IQuery{TResult}"/> rather than this interface, so that the pipeline knows whether the request changes state.
/// </summary>
/// <typeparam name="TResult">The outcome of the request: <see cref="Result"/> or <see cref="Result{T}"/>, so that middleware can inspect it.</typeparam>
/// <remarks>Requests are immutable records. Exactly one <see cref="IRequestHandler{TRequest, TResult}"/> handles each request type.</remarks>
public interface IRequest<TResult>;

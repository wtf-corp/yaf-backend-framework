using Microsoft.Extensions.DependencyInjection;
using YAF.Core.Results;
using YAF.Domain.Events;

namespace YAF.Application.Registration;

/// <summary>
/// The handlers of one assembly: which request types it declares and handles, how its handlers are registered, and a switch from a
/// request's runtime type to the <see cref="IRequestRunner"/> call that runs its handler.
/// </summary>
/// <remarks>
/// Emitted by the generator; do not implement. The assembly names its registry in <see cref="YafHandlersAttribute"/>, and
/// <see cref="YafApplicationBuilder.AddHandlersFrom{TMarker}"/> creates it once. At startup every declared request must be handled by
/// exactly one registered registry, or the host does not start.
/// </remarks>
public interface IHandlerRegistry
{
    /// <summary>Gets the request types the assembly declares (commands, queries and stream queries); never <see langword="null"/>.</summary>
    IReadOnlyCollection<Type> DeclaredRequests { get; }

    /// <summary>Gets the request types the assembly has a handler for; never <see langword="null"/>.</summary>
    IReadOnlyCollection<Type> HandledRequests { get; }

    /// <summary>Adds the assembly's handlers and validators to <paramref name="services"/>.</summary>
    /// <param name="services">The application's services.</param>
    void Register(IServiceCollection services);

    /// <summary>Runs the handler of <paramref name="request"/> through <paramref name="runner"/> when this assembly handles it.</summary>
    /// <typeparam name="TResult">The outcome of the request.</typeparam>
    /// <param name="request">The command or query.</param>
    /// <param name="runner">Runs the pipeline and the handler in the caller's scope.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <param name="result">The running request when this method returns <see langword="true"/>; otherwise <see langword="default"/>.</param>
    /// <returns><see langword="true"/> when this assembly handles the request type; otherwise <see langword="false"/>.</returns>
    bool TrySend<TResult>(IRequest<TResult> request, IRequestRunner runner, CancellationToken ct, out ValueTask<TResult> result);

    /// <summary>Streams the handler of <paramref name="query"/> through <paramref name="runner"/> when this assembly handles it.</summary>
    /// <typeparam name="TItem">The type of the streamed items.</typeparam>
    /// <param name="query">The stream query.</param>
    /// <param name="runner">Runs the query pipeline and the handler in the caller's scope.</param>
    /// <param name="ct">Cancels the enumeration.</param>
    /// <param name="items">The items when this method returns <see langword="true"/>; otherwise an empty sequence.</param>
    /// <returns><see langword="true"/> when this assembly handles the query type; otherwise <see langword="false"/>.</returns>
    bool TryStream<TItem>(IStreamQuery<TItem> query, IRequestRunner runner, CancellationToken ct, out IAsyncEnumerable<TItem> items);

    /// <summary>Runs this assembly's handlers of <paramref name="domainEvent"/> through <paramref name="runner"/>.</summary>
    /// <param name="domainEvent">The domain event.</param>
    /// <param name="runner">Runs the handlers in registration order in the caller's scope.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <param name="result">The running handlers when this method returns <see langword="true"/>; otherwise <see langword="default"/>.</param>
    /// <returns><see langword="true"/> when this assembly handles the event type; otherwise <see langword="false"/>.</returns>
    bool TryPublish(IDomainEvent domainEvent, IRequestRunner runner, CancellationToken ct, out ValueTask<Result> result);
}

using Microsoft.Extensions.Hosting;

namespace YAF.Application.Registration;

/// <summary>
/// Refuses to start when a request type is handled by more than one registry, or declared by a registry and handled by none (<c>04</c> §5
/// item 1). Reports every such request together.
/// </summary>
internal sealed class HandlerStartupCheck(ApplicationRegistrations registrations) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var handledBy = registrations.Registries
            .SelectMany(registry => registry.HandledRequests.Distinct().Select(request => (Request: request, Registry: registry)))
            .ToLookup(entry => entry.Request, entry => entry.Registry);

        var duplicates = handledBy
            .Where(group => group.Count() > 1)
            .Select(group => $"Request {group.Key.FullName} is handled by more than one handler registry: "
                + string.Join(", ", group.Select(registry => registry.GetType().AssemblyQualifiedName)) + ".");

        var missing = registrations.Registries
            .SelectMany(registry => registry.DeclaredRequests)
            .Distinct()
            .Where(request => !handledBy.Contains(request))
            .Select(request => $"Request {request.FullName} has no handler in any registered handler registry; "
                + "call AddHandlersFrom<TMarker>() for the assembly that handles it.");

        var problems = duplicates.Concat(missing).Order(StringComparer.Ordinal).ToList();
        return problems.Count == 0
            ? Task.CompletedTask
            : throw new InvalidOperationException(string.Join(Environment.NewLine, problems));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

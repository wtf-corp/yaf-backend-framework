using YAF.Application.UnitOfWork;
using YAF.Domain;
using YAF.Domain.Events;

namespace Testing.Tests.Fixtures;

/// <summary>A hand-written tracker that records every call to <see cref="Track"/>, so a test sees each one.</summary>
internal sealed class RecordingTracker : IAggregateTracker
{
    private readonly List<IAggregateRoot> _tracked = [];

    public IReadOnlyList<IAggregateRoot> Tracked => _tracked;

    public void Track(IAggregateRoot aggregate) => _tracked.Add(aggregate);

    public IReadOnlyList<IDomainEvent> DequeueDomainEvents() => [.. _tracked.SelectMany(aggregate => aggregate.DequeueDomainEvents())];

    public void Clear() => _tracked.Clear();
}

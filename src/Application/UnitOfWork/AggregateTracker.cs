using YAF.Domain;
using YAF.Domain.Events;

namespace YAF.Application.UnitOfWork;

/// <summary>Tracks aggregates by reference, each once, in the order they were first tracked.</summary>
internal sealed class AggregateTracker : IAggregateTracker
{
    private readonly List<IAggregateRoot> _tracked = [];
    private readonly HashSet<IAggregateRoot> _seen = new(ReferenceEqualityComparer.Instance);

    public AggregateTracker()
    {
        Tracked = _tracked.AsReadOnly();
    }

    /// <inheritdoc/>
    public IReadOnlyList<IAggregateRoot> Tracked { get; }

    /// <inheritdoc/>
    public void Track(IAggregateRoot aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (_seen.Add(aggregate))
        {
            _tracked.Add(aggregate);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<IDomainEvent> DequeueDomainEvents()
    {
        List<IDomainEvent> events = [];
        foreach (var aggregate in _tracked)
        {
            events.AddRange(aggregate.DequeueDomainEvents());
        }

        return events;
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _tracked.Clear();
        _seen.Clear();
    }
}

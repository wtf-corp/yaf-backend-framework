using System.Collections.ObjectModel;
using YAF.Core.Results;

namespace YAF.Domain.StateMachines;

/// <summary>
/// The allowed transitions between the states of an aggregate, defined once and checked by the aggregate's behaviours.
/// </summary>
/// <remarks>
/// Hold a machine in a <c>static readonly</c> field of the aggregate. The definition is immutable and side-effect free; a guard that needs aggregate
/// data is a business rule checked in the behaviour, not part of the definition. A mistake in the definition throws when the field is initialised,
/// that is when the type is first used.
/// </remarks>
/// <example>
/// <code language="csharp">
/// public static readonly StateMachine&lt;SessionStatus, SessionTrigger&gt; Lifecycle =
///     StateMachine&lt;SessionStatus, SessionTrigger&gt;.Define(b =&gt; b
///         .From(SessionStatus.Draft).On(SessionTrigger.Publish).To(SessionStatus.Published)
///         .From(SessionStatus.Published).On(SessionTrigger.Cancel).To(SessionStatus.Cancelled)
///         .Otherwise(Errors.TransitionNotAllowed));
///
/// public Result Publish() =&gt; Lifecycle.Fire(Status, SessionTrigger.Publish).Map(next =&gt; Status = next);
/// </code>
/// </example>
/// <typeparam name="TState">The state type: an enum or an <see cref="Enumeration{TSelf}"/>.</typeparam>
/// <typeparam name="TTrigger">The trigger type, normally an enum.</typeparam>
public sealed class StateMachine<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    private readonly Dictionary<(TState From, TTrigger Trigger), TState> _targets = [];
    private readonly Dictionary<TState, List<TTrigger>> _triggers = [];
    private readonly List<TState> _states = [];
    private readonly ErrorDescriptor _otherwise;

    internal StateMachine(IEnumerable<(TState From, TTrigger Trigger, TState To)> transitions, ErrorDescriptor otherwise)
    {
        _otherwise = otherwise;
        foreach (var (from, trigger, to) in transitions)
        {
            _targets.Add((from, trigger), to);
            if (!_triggers.TryGetValue(from, out var triggers))
            {
                _triggers.Add(from, triggers = []);
            }

            triggers.Add(trigger);
            AddState(from);
            AddState(to);
        }

        States = _states.AsReadOnly();
    }

    /// <summary>Every state the definition names, in the order in which each first appears.</summary>
    public IReadOnlyList<TState> States { get; }

    /// <summary>Defines a state machine.</summary>
    /// <param name="definition">
    /// A function that names each transition with <see cref="StateMachineBuilder{TState, TTrigger}.From"/>,
    /// <see cref="StateMachineBuilder{TState, TTrigger}.On"/> and <see cref="StateMachineBuilder{TState, TTrigger}.To"/>, in that order, and
    /// ends with <see cref="StateMachineBuilder{TState, TTrigger}.Otherwise"/>.
    /// </param>
    /// <returns>The state machine.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The definition calls the builder out of order, defines a second target for the same state and trigger, or returns no machine.
    /// </exception>
    public static StateMachine<TState, TTrigger> Define(Func<StateMachineBuilder<TState, TTrigger>, StateMachine<TState, TTrigger>> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition(new StateMachineBuilder<TState, TTrigger>())
            ?? throw new InvalidOperationException("The definition of a state machine must return the machine that Otherwise creates.");
    }

    /// <summary>Finds the state that a trigger leads to from the current state.</summary>
    /// <param name="current">The current state.</param>
    /// <param name="trigger">The trigger.</param>
    /// <returns>
    /// The target state; or, when the definition has no such transition, a failure with the <c>Otherwise</c> error, whose arguments are
    /// <c>From</c>, the current state, and <c>Trigger</c>.
    /// </returns>
    public Result<TState> Fire(TState current, TTrigger trigger) =>
        _targets.TryGetValue((current, trigger), out var target) ? target : _otherwise.With(("From", current), ("Trigger", trigger));

    /// <summary>Lists the triggers the definition allows from a state, for example to tell a client what it may do next.</summary>
    /// <param name="current">The current state.</param>
    /// <returns>The triggers in definition order; empty when the state has no outgoing transition.</returns>
    public IReadOnlyList<TTrigger> PermittedTriggers(TState current) =>
        _triggers.TryGetValue(current, out var triggers) ? triggers.AsReadOnly() : ReadOnlyCollection<TTrigger>.Empty;

    private void AddState(TState state)
    {
        if (!_states.Contains(state))
        {
            _states.Add(state);
        }
    }
}

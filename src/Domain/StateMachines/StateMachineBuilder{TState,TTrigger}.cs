using YAF.Core.Results;

namespace YAF.Domain.StateMachines;

/// <summary>
/// Collects the transitions of a <see cref="StateMachine{TState, TTrigger}"/>; it is the parameter of the function passed to
/// <see cref="StateMachine{TState, TTrigger}.Define"/>.
/// </summary>
/// <remarks>
/// Each transition is written as <see cref="From"/>, <see cref="On"/>, <see cref="To"/>, in that order; <see cref="Otherwise"/> ends the
/// definition. A call out of order throws <see cref="InvalidOperationException"/>.
/// </remarks>
/// <typeparam name="TState">The state type.</typeparam>
/// <typeparam name="TTrigger">The trigger type.</typeparam>
public sealed class StateMachineBuilder<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    private readonly List<(TState From, TTrigger Trigger, TState To)> _transitions = [];
    private TState? _from;
    private TTrigger? _trigger;
    private Step _step;

    internal StateMachineBuilder()
    {
    }

    private enum Step
    {
        Ready,
        From,
        On,
    }

    /// <summary>Starts a transition from a state.</summary>
    /// <param name="state">The state the transition leaves.</param>
    /// <returns>This builder, for <see cref="On"/>.</returns>
    /// <exception cref="InvalidOperationException">The previous transition is not finished with <see cref="To"/>.</exception>
    public StateMachineBuilder<TState, TTrigger> From(TState state)
    {
        ThrowIfUnfinished();
        (_from, _step) = (state, Step.From);
        return this;
    }

    /// <summary>Names the trigger of the transition started with <see cref="From"/>.</summary>
    /// <param name="trigger">The trigger.</param>
    /// <returns>This builder, for <see cref="To"/>.</returns>
    /// <exception cref="InvalidOperationException">No <see cref="From"/> comes directly before.</exception>
    public StateMachineBuilder<TState, TTrigger> On(TTrigger trigger)
    {
        if (_step != Step.From)
        {
            throw new InvalidOperationException($"On({trigger}) must follow From(state) in the definition of a state machine.");
        }

        (_trigger, _step) = (trigger, Step.On);
        return this;
    }

    /// <summary>Finishes the transition with its target state.</summary>
    /// <param name="state">The state the transition leads to.</param>
    /// <returns>This builder, for the next <see cref="From"/> or for <see cref="Otherwise"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// No <see cref="On"/> comes directly before, or the state and trigger already have a target.
    /// </exception>
    public StateMachineBuilder<TState, TTrigger> To(TState state)
    {
        if (_step != Step.On)
        {
            throw new InvalidOperationException(_step == Step.From
                ? $"To({state}) after From({_from}) needs On(trigger) between them in the definition of a state machine."
                : $"To({state}) must follow From(state).On(trigger) in the definition of a state machine.");
        }

        var comparer = EqualityComparer<TState>.Default;
        var triggers = EqualityComparer<TTrigger>.Default;
        if (_transitions.Exists(t => comparer.Equals(t.From, _from) && triggers.Equals(t.Trigger, _trigger)))
        {
            throw new InvalidOperationException($"State {_from} already has a transition on {_trigger}; a state and trigger have one target.");
        }

        _transitions.Add((_from!, _trigger!, state));
        _step = Step.Ready;
        return this;
    }

    /// <summary>Ends the definition with the error that <see cref="StateMachine{TState, TTrigger}.Fire"/> returns for an undefined transition.</summary>
    /// <param name="error">
    /// The error, declared in an <see cref="IErrorCatalog"/>. Its message may use the placeholders <c>{From}</c> and <c>{Trigger}</c>.
    /// </param>
    /// <returns>The state machine.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The last transition is not finished with <see cref="To"/>.</exception>
    public StateMachine<TState, TTrigger> Otherwise(ErrorDescriptor error)
    {
        ArgumentNullException.ThrowIfNull(error);
        ThrowIfUnfinished();
        return new StateMachine<TState, TTrigger>(_transitions, error);
    }

    private void ThrowIfUnfinished()
    {
        if (_step != Step.Ready)
        {
            throw new InvalidOperationException($"The transition from {_from} is not finished: it needs On(trigger).To(state).");
        }
    }
}

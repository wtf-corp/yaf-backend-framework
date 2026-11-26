using YAF.Domain.StateMachines;

namespace YAF.Testing;

/// <summary>
/// Asserts that a state machine is well formed: every state is reachable from the initial state, and every state that is not final has an exit. A
/// failed assertion throws <see cref="YafAssertionException"/>, whose message names the offending states and nothing else.
/// </summary>
/// <example>
/// <code>
/// StateMachineAssert.IsWellFormed(Session.Lifecycle, SessionStatus.Draft, [SessionStatus.Cancelled, SessionStatus.Completed]);
/// </code>
/// </example>
public static class StateMachineAssert
{
    /// <summary>
    /// Asserts that every state of <paramref name="machine"/> is reachable from <paramref name="initial"/> and that every state not in
    /// <paramref name="finals"/> has a transition to another state.
    /// </summary>
    /// <remarks>
    /// The states checked are every value of the enum when <typeparamref name="TState"/> is an enum, so that a value the definition forgets is
    /// reported as unreachable; otherwise they are <see cref="StateMachine{TState, TTrigger}.States"/>. A transition from a state to itself is not
    /// an exit.
    /// </remarks>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <typeparam name="TTrigger">The trigger type.</typeparam>
    /// <param name="machine">The state machine to check.</param>
    /// <param name="initial">The state every other state must be reachable from.</param>
    /// <param name="finals">The states that need no exit.</param>
    /// <exception cref="ArgumentNullException"><paramref name="machine"/>, <paramref name="initial"/> or <paramref name="finals"/> is <see langword="null"/>.</exception>
    /// <exception cref="YafAssertionException">
    /// A state is unreachable or a non-final state has no exit; the message names the unreachable states and the states without an exit.
    /// </exception>
    public static void IsWellFormed<TState, TTrigger>(StateMachine<TState, TTrigger> machine, TState initial, IReadOnlyCollection<TState> finals)
        where TState : notnull
        where TTrigger : notnull
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(finals);

        var states = typeof(TState).IsEnum ? Enum.GetValues(typeof(TState)).Cast<TState>().ToList() : machine.States;
        var reached = new HashSet<TState> { initial };
        var pending = new Queue<TState>([initial]);
        while (pending.TryDequeue(out var state))
        {
            foreach (var trigger in machine.PermittedTriggers(state))
            {
                var target = machine.Fire(state, trigger).Value;
                if (reached.Add(target))
                {
                    pending.Enqueue(target);
                }
            }
        }

        var unreachable = states.Where(state => !reached.Contains(state)).ToList();
        var withoutExit = states
            .Where(state => !finals.Contains(state)
                && !machine.PermittedTriggers(state).Any(trigger => !EqualityComparer<TState>.Default.Equals(machine.Fire(state, trigger).Value, state)))
            .ToList();
        var problems = new List<string>();
        if (unreachable.Count > 0)
        {
            problems.Add($"the states {Names(unreachable)} are unreachable from '{initial}'");
        }

        if (withoutExit.Count > 0)
        {
            problems.Add($"the non-final states {Names(withoutExit)} have no exit");
        }

        if (problems.Count > 0)
        {
            throw new YafAssertionException(
                $"Expected the state machine of {typeof(TState).Name} to be well formed, but {string.Join(" and ", problems)}.");
        }
    }

    private static string Names<TState>(IEnumerable<TState> states) => string.Join(", ", states.Select(state => $"'{state}'"));
}

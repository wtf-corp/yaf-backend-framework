namespace YAF.Application.Context;

/// <summary>Holds one scope's execution context: <see cref="Begin"/> sets it once; <see cref="Current"/> throws when there is none.</summary>
internal sealed class ExecutionContextScope : IExecutionContextAccessor, IExecutionContextScopeFactory
{
    private IExecutionContext? _current;
    private int _begun;

    /// <inheritdoc/>
    public IExecutionContext Current => Volatile.Read(ref _current) ?? throw new MissingExecutionContextException();

    /// <inheritdoc/>
    public IDisposable Begin(ExecutionContextSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(seed.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(seed.Origin);
        var context = new SeededExecutionContext(seed);
        if (Interlocked.Exchange(ref _begun, 1) != 0)
        {
            throw new InvalidOperationException("The execution context of this scope was already set.");
        }

        Volatile.Write(ref _current, context);
        return new End(this);
    }

    private sealed class End(ExecutionContextScope scope) : IDisposable
    {
        public void Dispose() => Volatile.Write(ref scope._current, null);
    }
}

namespace YAF.Application.Context;

/// <summary>
/// Holds the execution context of one scope: <see cref="Begin"/> sets it once, <see cref="Current"/> reads it. Without a context, or after it
/// ended, reading it throws <see cref="MissingExecutionContextException"/>.
/// </summary>
internal sealed class ExecutionContextScope : IExecutionContextAccessor, IExecutionContextScopeFactory
{
    private IExecutionContext? _current;
    private int _begun;

    /// <inheritdoc/>
    public IExecutionContext Current => Volatile.Read(ref _current) ?? throw new MissingExecutionContextException();

    /// <inheritdoc/>
    public IDisposable Begin(ExecutionContextSeed seed)
    {
        var context = new SeededExecutionContext(seed);
        if (Interlocked.Exchange(ref _begun, 1) != 0)
        {
            throw new InvalidOperationException("The execution context of this scope was already set.");
        }

        Volatile.Write(ref _current, context);
        return new End(this);
    }

    private void Clear() => Volatile.Write(ref _current, null);

    private sealed class End(ExecutionContextScope scope) : IDisposable
    {
        public void Dispose() => scope.Clear();
    }
}

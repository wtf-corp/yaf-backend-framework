namespace YAF.Application.Context;

/// <summary>
/// Establishes the execution context of a scope. Only the framework's entry points use it: the HTTP middleware, the consumers, the job runner,
/// the cron trigger and the test host (<c>04</c> §7).
/// </summary>
internal interface IExecutionContextScopeFactory
{
    /// <summary>Sets the context of the scope from <paramref name="seed"/>. A scope gets its context once.</summary>
    /// <param name="seed">What the entry point knows about the caller and the flow of work.</param>
    /// <returns>An object that ends the context when disposed; afterwards the scope has none.</returns>
    /// <exception cref="InvalidOperationException">The scope's context was already set.</exception>
    IDisposable Begin(ExecutionContextSeed seed);
}

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using YAF.Application;
using YAF.Core.Results;

namespace Benchmarks.Cases.Application;

/// <summary>
/// Shows what YAF's dispatcher adds to a handler call: a query sent through <see cref="IDispatcher"/> with an empty pipeline, against the
/// same query passed to its handler directly (04 §11, informational: less than 100 ns and no allocation beyond the pipeline's own).
/// </summary>
/// <remarks>
/// The dispatcher is resolved once from one scope, as an entry point resolves it for a request; creating the scope is not measured.
/// </remarks>
[MemoryDiagnoser]
public class DispatcherBenchmarks
{
    private readonly PingQuery _query = new(1);
    private readonly PingQueryHandler _handler = new();
    private readonly ServiceProvider _services = new ServiceCollection()
        .AddYafApplication(app => app.AddHandlersFrom<DispatcherBenchmarks>())
        .BuildServiceProvider();

    private readonly IServiceScope _scope;
    private readonly IDispatcher _dispatcher;

    /// <summary>Initializes a new instance of the <see cref="DispatcherBenchmarks"/> class with the dispatcher of one scope.</summary>
    public DispatcherBenchmarks()
    {
        _scope = _services.CreateScope();
        _dispatcher = _scope.ServiceProvider.GetRequiredService<IDispatcher>();
    }

    /// <summary>The handler called directly: the cost without a dispatcher.</summary>
    /// <returns>The handler's result, so that the call is not optimised away.</returns>
    [Benchmark(Baseline = true)]
    public ValueTask<Result<int>> DirectHandlerCall() => _handler.HandleAsync(_query, CancellationToken.None);

    /// <summary>The same query through the dispatcher: the generated registry's switch, the handler's resolution and the empty pipeline.</summary>
    /// <returns>The handler's result, so that the call is not optimised away.</returns>
    [Benchmark]
    public ValueTask<Result<int>> SendThroughDispatcher() => _dispatcher.SendAsync(_query, CancellationToken.None);

    /// <summary>Disposes the scope and the service provider.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _scope.Dispose();
        _services.Dispose();
    }
}

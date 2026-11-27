using Microsoft.Extensions.DependencyInjection;
using YAF.Application;
using YAF.Application.Pipeline;
using YAF.Application.Registration;

namespace Application.Tests.Dispatching;

public sealed class DispatcherTests
{
    [Fact]
    public async Task SendAsync_RunsMiddlewareOutermostFirstAroundTheHandler()
    {
        using var scope = Scope(p => p.Use(typeof(OuterMiddleware<,>)).Use(typeof(InnerMiddleware<,>)));
        var services = scope.ServiceProvider;

        await services.GetRequiredService<IDispatcher>().SendAsync(new PingCommand(), TestContext.Current.CancellationToken);

        services.GetRequiredService<CallLog>().Entries.Should().Equal("outer:before", "inner:before", "handler", "inner:after", "outer:after");
    }

    [Fact]
    public async Task SendAsync_MiddlewareShortCircuits_HandlerNotRun()
    {
        using var scope = Scope(p => p.Use(typeof(OuterMiddleware<,>)).Use<ShortCircuitMiddleware>());
        var services = scope.ServiceProvider;

        var result = await services.GetRequiredService<IDispatcher>().SendAsync(new PingCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        services.GetRequiredService<CallLog>().Entries.Should().Equal("outer:before", "outer:after");
    }

    [Fact]
    public async Task SendAsync_MiddlewareRetries_HandlerRunsTwice()
    {
        using var scope = Scope(p => p.Use<RetryMiddleware>().Use(typeof(InnerMiddleware<,>)));
        var services = scope.ServiceProvider;

        var result = await services.GetRequiredService<IDispatcher>().SendAsync(new PingCommand(FailOnce: true), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        services.GetRequiredService<CallLog>().Entries.Should().Equal("inner:before", "handler", "inner:before", "handler", "inner:after");
    }

    [Fact]
    public async Task SendAsync_RunsThePipelineOnce()
    {
        using var scope = Scope(p => p.Use(typeof(OuterMiddleware<,>)));
        var services = scope.ServiceProvider;

        await services.GetRequiredService<IDispatcher>().SendAsync(new PingQuery(), TestContext.Current.CancellationToken);

        services.GetRequiredService<CallLog>().Entries.Should().Equal("outer:before", "handler", "outer:after");
    }

    [Fact]
    public async Task SendAsync_HandlerAndMiddlewareShareTheScope()
    {
        var provider = Provider(p => p.Use(typeof(OuterMiddleware<,>)));
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var first = firstScope.ServiceProvider;
        var second = secondScope.ServiceProvider;

        await first.GetRequiredService<IDispatcher>().SendAsync(new PingCommand(), TestContext.Current.CancellationToken);

        first.GetRequiredService<CallLog>().Entries.Should().Equal("outer:before", "handler", "outer:after");
        second.GetRequiredService<CallLog>().Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_PassesCancellationToTheHandler()
    {
        using var scope = Scope(p => p.Use(typeof(OuterMiddleware<,>)));
        var services = scope.ServiceProvider;
        using var cancellation = new CancellationTokenSource();

        await services.GetRequiredService<IDispatcher>().SendAsync(new PingCommand(), cancellation.Token);

        services.GetRequiredService<CallLog>().HandlerToken.Should().Be(cancellation.Token);
    }

    [Fact]
    public async Task SendAsync_CommandScopedStep_SkippedForQueries()
    {
        using var scope = Scope(p => p.Use(typeof(OuterMiddleware<,>)).ForCommands(c => c.Use(typeof(InnerMiddleware<,>))));
        var services = scope.ServiceProvider;

        await services.GetRequiredService<IDispatcher>().SendAsync(new PingQuery(), TestContext.Current.CancellationToken);

        services.GetRequiredService<CallLog>().Entries.Should().Equal("outer:before", "handler", "outer:after");
    }

    [Fact]
    public async Task SendAsync_CommandScopedStep_RunsForCommands()
    {
        using var scope = Scope(p => p.ForCommands(c => c.Use(typeof(InnerMiddleware<,>))));
        var services = scope.ServiceProvider;

        await services.GetRequiredService<IDispatcher>().SendAsync(new PingCommand(), TestContext.Current.CancellationToken);

        services.GetRequiredService<CallLog>().Entries.Should().Equal("inner:before", "handler", "inner:after");
    }

    private static IServiceScope Scope(Action<PipelineBuilder> pipeline) => Provider(pipeline).CreateScope();

    /// <summary>The services <c>AddYafApplication</c> registers, with the hand-written registry in place of a generated one.</summary>
    internal static TestServiceProvider Provider(Action<PipelineBuilder> pipeline)
    {
        var services = new ServiceCollection().AddYafApplication(app => app.UsePipeline(pipeline));
        var registry = new DispatchingRegistry();
        registry.Register(services);
        DispatchingRegistry.RegisterMiddleware(services);
        ((ApplicationRegistrations)services.Single(service => service.ServiceType == typeof(ApplicationRegistrations)).ImplementationInstance!)
            .Add(registry);
        return TestServiceProvider.Build(services);
    }
}

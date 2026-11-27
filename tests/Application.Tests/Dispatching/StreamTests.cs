using Microsoft.Extensions.DependencyInjection;
using YAF.Application;
using YAF.Application.Pipeline;
using YAF.Core.Results;

namespace Application.Tests.Dispatching;

public sealed class StreamTests
{
    [Fact]
    public async Task StreamAsync_RunsQueryMiddlewareAroundTheHandler()
    {
        using var scope = Scope(p => p.Use(typeof(OuterMiddleware<,>)).ForQueries(q => q.Use(typeof(InnerMiddleware<,>))));
        var services = scope.ServiceProvider;
        var log = services.GetRequiredService<CallLog>();

        await foreach (var item in services.GetRequiredService<IDispatcher>().StreamAsync(new CountQuery(2), TestContext.Current.CancellationToken))
        {
            log.Entries.Add($"caller:{item}");
        }

        log.Entries.Should().Equal(
            "outer:before", "inner:before", "handler:1", "caller:1", "handler:2", "caller:2", "inner:after", "outer:after");
    }

    [Fact]
    public async Task StreamAsync_YieldsTheHandlersItems()
    {
        using var scope = Scope(p => p.Use(typeof(OuterMiddleware<,>)));
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var items = await dispatcher.StreamAsync(new CountQuery(3), TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        items.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task StreamAsync_PassesCancellation()
    {
        using var scope = Scope(p => p.Use(typeof(OuterMiddleware<,>)));
        var services = scope.ServiceProvider;
        var log = services.GetRequiredService<CallLog>();
        using var cancellation = new CancellationTokenSource();

        var dispatcher = services.GetRequiredService<IDispatcher>();

        var enumerate = async () =>
        {
            await foreach (var item in dispatcher.StreamAsync(new CountQuery(10), cancellation.Token).ConfigureAwait(false))
            {
                log.Entries.Add($"caller:{item}");
                await cancellation.CancelAsync().ConfigureAwait(false);
            }
        };

        await enumerate.Should().ThrowAsync<OperationCanceledException>();
        log.HandlerToken.IsCancellationRequested.Should().BeTrue();
        log.Entries.Should().Equal("outer:before", "handler:1", "caller:1");
    }

    [Fact]
    public async Task StreamAsync_MiddlewareShortCircuitsWithFailure_Throws()
    {
        using var scope = Scope(p => p.Use(typeof(OuterMiddleware<,>)).Use(typeof(RefusingMiddleware<,>)));
        var services = scope.ServiceProvider;

        var enumerate = async () => await services.GetRequiredService<IDispatcher>()
            .StreamAsync(new CountQuery(2), TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

        await enumerate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{DispatchErrors.Refused.Code}*");
        services.GetRequiredService<CallLog>().Entries.Should().Equal("outer:before", "outer:after");
    }

    [Fact]
    public async Task StreamAsync_CommandScopedStep_Skipped()
    {
        using var scope = Scope(p => p.Use(typeof(OuterMiddleware<,>)).ForCommands(c => c.Use(typeof(InnerMiddleware<,>))));
        var services = scope.ServiceProvider;

        await services.GetRequiredService<IDispatcher>().StreamAsync(new CountQuery(1), TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        services.GetRequiredService<CallLog>().Entries.Should().Equal("outer:before", "handler:1", "outer:after");
    }

    private static IServiceScope Scope(Action<PipelineBuilder> pipeline) => DispatcherTests.Provider(pipeline).CreateScope();
}

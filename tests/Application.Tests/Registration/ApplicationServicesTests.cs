using Application.Tests.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;
using YAF.Application;
using YAF.Application.Registration;
using YAF.Application.UnitOfWork;
using YAF.Testing;

namespace Application.Tests.Registration;

public sealed class ApplicationServicesTests
{
    [Fact]
    public async Task AddYafApplication_ResolvesThePublisherInAScope()
    {
        var log = new HandlerLog();
        var services = WithPublishingRegistry(log);
        using var provider = Build(services);
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>()
            .PublishAsync([new SessionScheduled(1)], TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        log.Entries.Should().Equal("first:1");
    }

    [Fact]
    public void AddYafApplication_TrackerIsOnePerScope()
    {
        using var provider = Build(new ServiceCollection().AddYafApplication(_ => { }));
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var tracker = first.ServiceProvider.GetRequiredService<IAggregateTracker>();

        first.ServiceProvider.GetRequiredService<IAggregateTracker>().Should().BeSameAs(tracker);
        second.ServiceProvider.GetRequiredService<IAggregateTracker>().Should().NotBeSameAs(tracker);
    }

    [Fact]
    public void AddYafApplication_DispatcherIsOnePerScope()
    {
        using var provider = Build(new ServiceCollection().AddYafApplication(_ => { }));
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var dispatcher = first.ServiceProvider.GetRequiredService<IDispatcher>();

        first.ServiceProvider.GetRequiredService<Dispatcher>().Should().BeSameAs(dispatcher);
        second.ServiceProvider.GetRequiredService<IDispatcher>().Should().NotBeSameAs(dispatcher);
    }

    [Fact]
    public async Task AddYafApplication_DispatcherReplaced_PublisherKeepsYafsDispatcher()
    {
        var log = new HandlerLog();
        var services = WithPublishingRegistry(log);
        services.AddScoped<IDispatcher>(_ => new ReplacedDispatcher());
        using var provider = Build(services);
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>()
            .PublishAsync([new SessionScheduled(1)], TestContext.Current.CancellationToken);

        scope.ServiceProvider.GetRequiredService<IDispatcher>().Should().BeOfType<ReplacedDispatcher>();
        result.IsSuccess.Should().BeTrue();
        log.Entries.Should().Equal("first:1");
    }

    [Fact]
    public void AddYafApplication_NoTimeProvider_UsesTheSystemClock()
    {
        using var provider = Build(new ServiceCollection().AddYafApplication(_ => { }));

        provider.GetRequiredService<TimeProvider>().Should().BeSameAs(TimeProvider.System);
    }

    [Fact]
    public void AddYafApplication_TimeProviderRegisteredFirst_KeepsIt()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 11, 28, 9, 30, 0, TimeSpan.Zero));
        var services = new ServiceCollection().AddSingleton(clock.TimeProvider);

        services.AddYafApplication(_ => { });
        using var provider = Build(services);

        provider.GetRequiredService<TimeProvider>().Should().BeSameAs(clock.TimeProvider);
        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(TimeProvider));
    }

    private static ServiceProvider Build(IServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    /// <summary>The application layer with a registry that publishes <see cref="SessionScheduled"/> to one recording handler.</summary>
    private static IServiceCollection WithPublishingRegistry(HandlerLog log)
    {
        var services = new ServiceCollection().AddYafApplication(_ => { });
        services.Single(descriptor => descriptor.ServiceType == typeof(ApplicationRegistrations))
            .ImplementationInstance.Should().BeOfType<ApplicationRegistrations>().Which.Add(new PublishingRegistry());
        services.AddScoped<IDomainEventHandler<SessionScheduled>>(_ => new RecordingHandler(log, "first"));
        return services;
    }

    /// <summary>An app's own dispatcher, registered after YAF's; it handles nothing.</summary>
    private sealed class ReplacedDispatcher : IDispatcher
    {
        public ValueTask<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TItem> StreamAsync<TItem>(IStreamQuery<TItem> query, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}

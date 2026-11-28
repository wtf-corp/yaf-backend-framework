using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YAF.Application;
using YAF.Application.Pipeline;
using YAF.Application.Registration;
using YAF.Core.Results;

namespace Application.Tests.Registration;

public sealed class RegistrationTests
{
    [Fact]
    public void AddHandlersFrom_ReadsTheAssemblysRegistry()
    {
        var services = new ServiceCollection();

        services.AddYafApplication(app => app.AddHandlersFrom<RegistrationTests>());

        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(IRequestHandler<Dispatching.PingCommand, Result>)
            && descriptor.ImplementationType == typeof(Dispatching.PingCommandHandler) && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddHandlers_HandBuiltRegistry_RegistersItsServices()
    {
        var services = new ServiceCollection();

        services.AddYafApplication(app => app.AddHandlers(ThisAssembly, new HandBuiltRegistry()));

        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(PingHandlerStandIn));
    }

    [Fact]
    public void AddHandlers_SameAssemblyTwice_RegistersOnce()
    {
        var services = new ServiceCollection();

        services.AddYafApplication(app => app
            .AddHandlers(ThisAssembly, new HandBuiltRegistry())
            .AddHandlers(ThisAssembly, new HandBuiltRegistry()));

        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(PingHandlerStandIn));
        ((ApplicationRegistrations)Instance(services, typeof(ApplicationRegistrations))).Registries.Should().ContainSingle();
    }

    [Fact]
    public void AddHandlersFrom_AssemblyWithoutRegistry_ThrowsNamingIt()
    {
        var services = new ServiceCollection();

        var act = () => services.AddYafApplication(app => app.AddHandlersFrom<Result>());

        act.Should().Throw<InvalidOperationException>().WithMessage("*YAF.Core*YafHandlers*");
    }

    [Fact]
    public void AddYafApplication_CalledTwice_RegistersOnce()
    {
        var once = new ServiceCollection();
        once.AddYafApplication(app => app.AddHandlers(ThisAssembly, new HandBuiltRegistry()));
        var twice = new ServiceCollection();

        twice.AddYafApplication(app => app.AddHandlers(ThisAssembly, new HandBuiltRegistry()));
        twice.AddYafApplication(app => app.AddHandlers(ThisAssembly, new HandBuiltRegistry()));

        twice.Select(descriptor => descriptor.ServiceType).Should().Equal(once.Select(descriptor => descriptor.ServiceType));
    }

    [Fact]
    public void UsePipeline_RegistersTheConfiguredOrder()
    {
        var services = new ServiceCollection();
        var middleware = new PingMiddleware().GetType();

        services.AddYafApplication(app => app.UsePipeline(pipeline => pipeline.Use(middleware)));

        Descriptor(services).Steps.Should().Equal(new PipelineStep(middleware, PipelineScope.All));
    }

    [Fact]
    public async Task Startup_RequestHandledTwice_FailsNamingIt()
    {
        var check = StartupCheck(new HandBuiltRegistry([], [typeof(PingQuery)]), new HandBuiltRegistry([], [typeof(PingQuery)]));

        var act = () => check.StartAsync(TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage($"*{typeof(PingQuery).FullName}*more than one*");
    }

    [Fact]
    public async Task Startup_DeclaredRequestWithoutHandler_FailsNamingIt()
    {
        var check = StartupCheck(new HandBuiltRegistry([typeof(PingQuery), typeof(PongQuery)], [typeof(PingQuery)]));

        var act = () => check.StartAsync(TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage($"*{typeof(PongQuery).FullName}*no handler*");
    }

    [Fact]
    public async Task Startup_EachRequestHandledOnce_Starts()
    {
        var check = StartupCheck(new HandBuiltRegistry([typeof(PingQuery)], []), new HandBuiltRegistry([], [typeof(PingQuery)]));

        var act = () => check.StartAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    private static Assembly ThisAssembly => typeof(RegistrationTests).Assembly;

    /// <summary>The hosted service <c>AddYafApplication</c> registers, over the given registries (one assembly has only one registry).</summary>
    private static IHostedService StartupCheck(params IHandlerRegistry[] registries)
    {
        var services = new ServiceCollection().AddYafApplication(_ => { });
        Array.ForEach(registries, ((ApplicationRegistrations)Instance(services, typeof(ApplicationRegistrations))).Add);
        return (IHostedService)Instance(services, typeof(IHostedService));
    }

    private static object Instance(IServiceCollection services, Type serviceType) =>
        services.Single(descriptor => descriptor.ServiceType == serviceType).ImplementationInstance!;

    private static IPipelineDescriptor Descriptor(ServiceCollection services)
    {
        var descriptor = services.Single(service => service.ServiceType == typeof(IPipelineDescriptor));
        return (IPipelineDescriptor)descriptor.ImplementationFactory!(null!);
    }

    private sealed class PingMiddleware : IRequestMiddleware<PingQuery, Result>
    {
        public ValueTask<Result> InvokeAsync(PingQuery request, RequestDelegate<Result> next, CancellationToken ct) => next(ct);
    }
}

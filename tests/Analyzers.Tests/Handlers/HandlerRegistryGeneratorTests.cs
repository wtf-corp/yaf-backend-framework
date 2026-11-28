using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using YAF.Analyzers.Handlers;
using YAF.Application;
using YAF.Application.Registration;
using YAF.Application.Validation;
using YAF.Core.Results;
using YAF.Domain.Events;

namespace Analyzers.Tests.Handlers;

public sealed class HandlerRegistryGeneratorTests
{
    private const string Usings = """
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using YAF.Application;
        using YAF.Core.Results;

        """;

    [Fact]
    public async Task Handler_EmitsRegistryThatDispatchesItsRequest()
    {
        const string source = Usings + """
            namespace Probe;
            public sealed record Ping(string Name) : IQuery<Result<string>>;
            public sealed class PingHandler : IRequestHandler<Ping, Result<string>>
            {
                public ValueTask<Result<string>> HandleAsync(Ping request, CancellationToken ct) =>
                    ValueTask.FromResult(Result.Success("pong " + request.Name));
            }
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
        var (registry, services, runner) = Registered(result);
        var ping = Type(result, "Probe.Ping");
        registry.GetType().IsPublic.Should().BeFalse();
        registry.GetType().Name.Should().EndWith("YafHandlerRegistry");
        registry.DeclaredRequests.Should().Equal(ping);
        registry.HandledRequests.Should().Equal(ping);
        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(IRequestHandler<,>).MakeGenericType(ping, typeof(Result<string>))
            && descriptor.ImplementationType == Type(result, "Probe.PingHandler") && descriptor.Lifetime == ServiceLifetime.Scoped);

        var request = (IRequest<Result<string>>)Activator.CreateInstance(ping, "Ada")!;
        registry.TrySend(request, runner, TestContext.Current.CancellationToken, out var running).Should().BeTrue();
        (await running.ConfigureAwait(true)).Value.Should().Be("pong Ada");
        runner.Calls.Should().Equal("send Probe.Ping");
        registry.TrySend(new Unknown(), runner, TestContext.Current.CancellationToken, out _).Should().BeFalse();
    }

    [Fact]
    public async Task InternalHandler_IsRegistered()
    {
        const string source = Usings + """
            namespace Probe;
            internal sealed record Close : ICommand<Result>;
            internal static class Sessions
            {
                internal sealed class CloseHandler : IRequestHandler<Close, Result>
                {
                    public ValueTask<Result> HandleAsync(Close request, CancellationToken ct) => ValueTask.FromResult(Result.Success());
                }
            }
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
        var (registry, services, runner) = Registered(result);
        var close = Type(result, "Probe.Close");
        registry.HandledRequests.Should().Equal(close);
        services.Should().ContainSingle(descriptor => descriptor.ImplementationType == Type(result, "Probe.Sessions+CloseHandler")
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        registry.TrySend((IRequest<Result>)Activator.CreateInstance(close)!, runner, TestContext.Current.CancellationToken, out var running).Should().BeTrue();
        (await running.ConfigureAwait(true)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task StreamHandler_IsDispatched()
    {
        const string source = Usings + """
            namespace Probe;
            public sealed record Count(int To) : IStreamQuery<int>;
            internal sealed class CountHandler : IStreamQueryHandler<Count, int>
            {
                public async IAsyncEnumerable<int> HandleAsync(Count query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
                {
                    for (var i = 1; i <= query.To; i++)
                    {
                        await Task.Yield();
                        yield return i;
                    }
                }
            }
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
        var (registry, _, runner) = Registered(result);
        var count = Type(result, "Probe.Count");
        registry.DeclaredRequests.Should().Equal(count);
        registry.HandledRequests.Should().Equal(count);
        var query = (IStreamQuery<int>)Activator.CreateInstance(count, 3)!;
        registry.TryStream(query, runner, TestContext.Current.CancellationToken, out var items).Should().BeTrue();
        (await items.ToListAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).Should().Equal(1, 2, 3);
        runner.Calls.Should().Equal("stream Probe.Count");
        registry.TryStream(new Unknown(), runner, TestContext.Current.CancellationToken, out _).Should().BeFalse();
    }

    [Fact]
    public async Task DomainEventHandler_IsPublished()
    {
        const string source = Usings + """
            namespace Probe;
            public sealed record SessionOpened : YAF.Domain.Events.DomainEvent;
            internal sealed class NotifySpeakers : IDomainEventHandler<SessionOpened>
            {
                public ValueTask<Result> HandleAsync(SessionOpened domainEvent, CancellationToken ct) => ValueTask.FromResult(Result.Success());
            }
            internal sealed class UpdateAgenda : IDomainEventHandler<SessionOpened>
            {
                public ValueTask<Result> HandleAsync(SessionOpened domainEvent, CancellationToken ct) => ValueTask.FromResult(Result.Success());
            }
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
        var (registry, _, runner) = Registered(result);
        var opened = (IDomainEvent)Activator.CreateInstance(Type(result, "Probe.SessionOpened"))!;
        registry.TryPublish(opened, runner, TestContext.Current.CancellationToken, out var running).Should().BeTrue();
        (await running.ConfigureAwait(true)).IsSuccess.Should().BeTrue();
        runner.Calls.Should().Equal("publish Probe.SessionOpened", "handled by NotifySpeakers", "handled by UpdateAgenda");
        registry.TryPublish(new Unpublished(), runner, TestContext.Current.CancellationToken, out _).Should().BeFalse();
        registry.DeclaredRequests.Should().BeEmpty();
        registry.HandledRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task Validator_IsRegistered()
    {
        const string source = Usings + """
            namespace Probe;
            public sealed record Rename(string Title) : ICommand<Result>;
            internal sealed class RenameValidator : YAF.Application.Validation.Validator<Rename>;
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
        var (_, services, _) = Registered(result);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IValidator<>).MakeGenericType(Type(result, "Probe.Rename"))
            && descriptor.ImplementationType == Type(result, "Probe.RenameValidator") && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public async Task HandlerOfInaccessibleType_IsLeftOut()
    {
        const string source = Usings + """
            using YAF.Application.Validation;
            namespace Probe;
            public sealed record Open : ICommand<Result>;
            internal sealed class OpenHandler : IRequestHandler<Open, Result>
            {
                public ValueTask<Result> HandleAsync(Open request, CancellationToken ct) => ValueTask.FromResult(Result.Success());
            }
            internal static class Hidden
            {
                private sealed record Close : ICommand<Result>;
                private sealed record Count : IStreamQuery<int>;
                private sealed record Closed : YAF.Domain.Events.DomainEvent;
                internal sealed class CloseHandler : IRequestHandler<Close, Result>, IValidator<Close>
                {
                    ValueTask<Result> IRequestHandler<Close, Result>.HandleAsync(Close request, CancellationToken ct) => ValueTask.FromResult(Result.Success());
                    ValueTask<Result> IValidator<Close>.ValidateAsync(Close instance, CancellationToken ct) => ValueTask.FromResult(Result.Success());
                }
                internal sealed class CountHandler : IStreamQueryHandler<Count, int>
                {
                    IAsyncEnumerable<int> IStreamQueryHandler<Count, int>.HandleAsync(Count query, CancellationToken ct) => System.Linq.AsyncEnumerable.Empty<int>();
                }
                internal sealed class NotifyClosed : IDomainEventHandler<Closed>
                {
                    ValueTask<Result> IDomainEventHandler<Closed>.HandleAsync(Closed domainEvent, CancellationToken ct) => ValueTask.FromResult(Result.Success());
                }
            }
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
        var (registry, services, _) = Registered(result);
        var open = Type(result, "Probe.Open");
        registry.DeclaredRequests.Should().Equal(open);
        registry.HandledRequests.Should().Equal(open);
        services.Should().ContainSingle().Which.ImplementationType.Should().Be(Type(result, "Probe.OpenHandler"));
    }

    [Fact]
    public async Task DeclaredRequestWithoutHandler_IsListed()
    {
        const string source = Usings + """
            namespace Probe;
            public sealed record Open : ICommand<Result>;
            public sealed record List : IStreamQuery<string>;
            public abstract record Base : IQuery<Result>;
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
        var (registry, services, _) = Registered(result);
        registry.DeclaredRequests.Should().Equal(Type(result, "Probe.Open"), Type(result, "Probe.List"));
        registry.HandledRequests.Should().BeEmpty();
        services.Should().BeEmpty();
    }

    [Fact]
    public async Task NoHandlerOrRequest_EmitsNothing()
    {
        const string source = Usings + """
            namespace Probe;
            public sealed class Plain;
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
        result.Assembly!.GetCustomAttribute<YafHandlersAttribute>().Should().BeNull();
    }

    [Fact]
    public async Task FriendAssemblyWithHandlers_BuildsWithoutClash()
    {
        const string first = Usings + """
            [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Second")]
            namespace First;
            public sealed record Open : ICommand<Result>;
            internal sealed class OpenHandler : IRequestHandler<Open, Result>
            {
                public ValueTask<Result> HandleAsync(Open request, CancellationToken ct) => ValueTask.FromResult(Result.Success());
            }
            """;
        const string second = Usings + """
            namespace Second;
            public sealed record Close : ICommand<Result>;
            internal sealed class CloseHandler : IRequestHandler<Close, Result>
            {
                public ValueTask<Result> HandleAsync(Close request, CancellationToken ct) => ValueTask.FromResult(Result.Success());
            }
            """;

        using var firstResult = await RunAsync(first, "First");
        using var secondResult = await RunAsync(second, "Second", [MetadataReference.CreateFromImage(firstResult.Image)]);

        firstResult.Diagnostics.Should().BeEmpty();
        secondResult.Diagnostics.Should().BeEmpty();
        Registered(firstResult).Registry.HandledRequests.Should().Equal(Type(firstResult, "First.Open"));
        Registered(secondResult).Registry.HandledRequests.Should().Equal(Type(secondResult, "Second.Close"));
    }

    private static Task<HarnessResult> RunAsync(string source, string assemblyName = "Snippet", IEnumerable<MetadataReference>? references = null) =>
        GeneratorHarness.RunAsync(source, [new HandlerRegistryGenerator()], assemblyName: assemblyName, references: references);

    private static Type Type(HarnessResult result, string name) => result.Assembly!.GetType(name, throwOnError: true)!;

    /// <summary>The registry the assembly's attribute names, the services it registered, and a runner over them.</summary>
    private static (IHandlerRegistry Registry, IServiceCollection Services, RecordingRunner Runner) Registered(HarnessResult result)
    {
        var attribute = result.Assembly!.GetCustomAttribute<YafHandlersAttribute>();
        attribute.Should().NotBeNull("the generator emits [assembly: YafHandlers(...)]");
        var registry = (IHandlerRegistry)Activator.CreateInstance(attribute!.Registry)!;
        var services = new ServiceCollection();
        registry.Register(services);
        return (registry, services, new RecordingRunner(services));
    }

    private sealed record Unknown : IQuery<Result<string>>, IStreamQuery<int>;

    private sealed record Unpublished : DomainEvent;
}

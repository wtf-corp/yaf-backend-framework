using System.Reflection;
using YAF.Analyzers.ErrorCodes;
using YAF.Core.Results;

namespace Analyzers.Tests.ErrorCodes;

public sealed class ErrorRegistryGeneratorTests
{
    private static readonly Dictionary<string, string> s_catalog = new() { ["YafServiceCode"] = "catalog" };

    [Fact]
    public async Task Catalogue_EmitsServiceAttributeAndRegistry()
    {
        const string source = """
            using YAF.Core.Results;
            namespace Probe;
            public static class Session
            {
                public sealed class Errors : IErrorCatalog
                {
                    public static readonly ErrorDescriptor Full = Error.Conflict("Session is full.");
                }
            }
            """;

        using var result = await RunAsync(source, s_catalog);

        result.Diagnostics.Should().BeEmpty();
        var service = result.Assembly!.GetCustomAttribute<YafServiceAttribute>()!;
        service.ServiceCode.Should().Be("catalog");
        service.ErrorRegistry.FullName.Should().Be("YAF.Generated.YafErrorRegistry");
        Registry(result).Errors.Should().Equal(
            new ErrorRegistration(new ErrorDescriptor("catalog.session.full", ErrorKind.Conflict, "Session is full."), "Probe.Session+Errors", "Full"));
    }

    [Fact]
    public async Task Registry_ListsFieldsInDeclarationOrder()
    {
        const string source = """
            using YAF.Core.Results;
            namespace Probe;
            public sealed class SessionErrors : IErrorCatalog
            {
                public static readonly ErrorDescriptor Full = Error.Conflict("Session is full.");
                public static readonly ErrorDescriptor Closed = new("catalog.session.closed", ErrorKind.Conflict, "Session is closed."),
                    Cancelled = Error.Conflict("Session was cancelled.");
            }
            public sealed class SpeakerErrors : IErrorCatalog
            {
                public static readonly ErrorDescriptor Unknown = Error.NotFound("Speaker was not found.");
            }
            """;

        using var result = await RunAsync(source, s_catalog);

        result.Diagnostics.Should().BeEmpty();
        Registry(result).Errors.Select(error => error.Field).Should().Equal("Full", "Closed", "Cancelled", "Unknown");
        Registry(result).Errors.Select(error => error.Descriptor.Code).Should().Equal(
            "catalog.full", "catalog.session.closed", "catalog.cancelled", "catalog.unknown");
    }

    [Fact]
    public async Task NestedCatalogue_NamesTheDeclaringType()
    {
        const string source = """
            using YAF.Core.Results;
            namespace Probe.Sessions;
            public static class OuterArea
            {
                public static class Session
                {
                    internal sealed class Errors : IErrorCatalog
                    {
                        public static readonly ErrorDescriptor NoFreeSeats = Error.Conflict("Session has no free seats.");
                    }
                }
            }
            """;

        using var result = await RunAsync(source, s_catalog);

        result.Diagnostics.Should().BeEmpty();
        Registry(result).Errors.Should().ContainSingle()
            .Which.DeclaringType.Should().Be("Probe.Sessions.OuterArea+Session+Errors");
    }

    [Fact]
    public async Task PrivateCatalogue_IsLeftOut()
    {
        const string source = """
            using YAF.Core.Results;
            namespace Probe;
            public static class Session
            {
                public sealed class Errors : IErrorCatalog
                {
                    public static readonly ErrorDescriptor Full = Error.Conflict("Session is full.");
                }

                private sealed class HiddenErrors : IErrorCatalog
                {
                    public static readonly ErrorDescriptor Hidden = Error.Conflict("Hidden.");
                }
            }
            """;

        using var result = await RunAsync(source, s_catalog);

        result.Diagnostics.Should().BeEmpty();
        Registry(result).Errors.Select(error => error.Field).Should().Equal("Full");
    }

    [Fact]
    public async Task NoServiceCode_EmitsNothing()
    {
        const string source = """
            using YAF.Core.Results;
            namespace Probe;
            public sealed class SessionErrors : IErrorCatalog
            {
                public static readonly ErrorDescriptor Closed = new("catalog.session.closed", ErrorKind.Conflict, "Session is closed.");
            }
            """;

        using var result = await RunAsync(source, new Dictionary<string, string>());

        result.Diagnostics.Should().BeEmpty();
        result.Assembly!.GetCustomAttribute<YafServiceAttribute>().Should().BeNull();
        result.Assembly!.GetType("YAF.Generated.YafErrorRegistry").Should().BeNull();
    }

    [Fact]
    public async Task NoCatalogue_EmitsNothing()
    {
        const string source = """
            using YAF.Core.Results;
            namespace Probe;
            public static class Holder
            {
                public static readonly ErrorDescriptor NotACatalogue = new("catalog.holder.not", ErrorKind.Failure, "Not a catalogue.");
            }
            """;

        using var result = await RunAsync(source, s_catalog);

        result.Diagnostics.Should().BeEmpty();
        result.Assembly!.GetCustomAttribute<YafServiceAttribute>().Should().BeNull();
        result.Assembly!.GetType("YAF.Generated.YafErrorRegistry").Should().BeNull();
    }

    private static Task<HarnessResult> RunAsync(string source, Dictionary<string, string> buildProperties) =>
        GeneratorHarness.RunAsync(source, [new ErrorCodeGenerator(), new ErrorRegistryGenerator()], buildProperties: buildProperties);

    private static IErrorRegistry Registry(HarnessResult result)
    {
        var service = result.Assembly!.GetCustomAttribute<YafServiceAttribute>()!;
        return (IErrorRegistry)Activator.CreateInstance(service.ErrorRegistry)!;
    }
}

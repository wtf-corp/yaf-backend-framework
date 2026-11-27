using System.Reflection;
using Microsoft.CodeAnalysis;
using YAF.Analyzers.ErrorCodes;
using YAF.Core.Results;

namespace Analyzers.Tests.ErrorCodes;

public sealed class ErrorRegistryGeneratorTests
{
    private static readonly Dictionary<string, string> s_catalog = new() { ["YafServiceCode"] = "catalog" };

    // An assembly with a catalogue that grants its internals to "Second".
    private const string FriendlyFirst = """
        using YAF.Core.Results;
        [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Second")]
        namespace First;
        public sealed class SessionErrors : IErrorCatalog
        {
            public static readonly ErrorDescriptor Full = Error.Conflict("Session is full.");
        }
        """;

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
        ShouldBeHiddenRegistry(service.ErrorRegistry);
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
        result.Assembly!.GetTypes().Should().NotContain(type => type.Name.EndsWith("YafErrorRegistry", StringComparison.Ordinal));
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
        result.Assembly!.GetTypes().Should().NotContain(type => type.Name.EndsWith("YafErrorRegistry", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FriendAssemblyWithCatalogue_BuildsWithoutClash()
    {
        const string second = """
            using YAF.Core.Results;
            namespace Second;
            public sealed class SpeakerErrors : IErrorCatalog
            {
                public static readonly ErrorDescriptor Unknown = Error.NotFound("Speaker was not found.");
            }
            """;

        using var firstResult = await RunAsync(FriendlyFirst, s_catalog, "First");
        using var secondResult = await RunAsync(
            second, new() { ["YafServiceCode"] = "speakers" }, "Second", [MetadataReference.CreateFromImage(firstResult.Image)]);

        firstResult.Diagnostics.Should().BeEmpty();
        secondResult.Diagnostics.Should().BeEmpty();
        Registry(firstResult).Errors.Select(error => error.DeclaringType).Should().Equal("First.SessionErrors");
        Registry(secondResult).Errors.Select(error => error.DeclaringType).Should().Equal("Second.SpeakerErrors");
    }

    [Fact]
    public async Task Catalogue_RegistryIsNotVisibleOutsideItsAssembly()
    {
        const string second = """
            namespace Second;
            public static class Reader
            {
                public static System.Type Registry => typeof(global::YAF.Generated.YafErrorRegistry);
            }
            """;

        using var firstResult = await RunAsync(FriendlyFirst, s_catalog, "First");
        using var secondResult = await RunAsync(
            second, new Dictionary<string, string>(), "Second", [MetadataReference.CreateFromImage(firstResult.Image)]);

        firstResult.Diagnostics.Should().BeEmpty();
        secondResult.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "CS0234");
    }

    private static Task<HarnessResult> RunAsync(
        string source,
        Dictionary<string, string> buildProperties,
        string assemblyName = "Snippet",
        IEnumerable<MetadataReference>? references = null) =>
        GeneratorHarness.RunAsync(
            source, [new ErrorCodeGenerator(), new ErrorRegistryGenerator()], buildProperties: buildProperties, assemblyName: assemblyName, references: references);

    private static void ShouldBeHiddenRegistry(Type registry)
    {
        registry.Should().Implement<IErrorRegistry>();
        registry.IsPublic.Should().BeFalse();
        registry.IsNestedPublic.Should().BeFalse();
        registry.Name.Should().EndWith("YafErrorRegistry");
    }

    private static IErrorRegistry Registry(HarnessResult result)
    {
        var service = result.Assembly!.GetCustomAttribute<YafServiceAttribute>()!;
        return (IErrorRegistry)Activator.CreateInstance(service.ErrorRegistry)!;
    }
}

using YAF.Analyzers.ErrorCodes;
using YAF.Core.Results;

namespace Analyzers.Tests.ErrorCodes;

public sealed class ErrorCodeGeneratorTests
{
    private static readonly Dictionary<string, string> s_catalog = new() { ["YafServiceCode"] = "catalog" };

    [Fact]
    public async Task NestedCatalogue_DefaultCode_IsSnakeCasePath()
    {
        const string source = """
            using YAF.Core.Results;
            namespace Probe.Sessions;
            public static class OuterArea
            {
                public static class Session
                {
                    public sealed class Errors : IErrorCatalog
                    {
                        public static readonly ErrorDescriptor NoFreeSeats = Error.Conflict("Session has no free seats.");
                    }
                }
            }
            """;

        using var result = await GeneratorHarness.RunAsync(source, [new ErrorCodeGenerator()], buildProperties: s_catalog);

        result.Diagnostics.Should().BeEmpty();
        Read(result, "Probe.Sessions.OuterArea+Session+Errors", "NoFreeSeats")
            .Should().Be(new ErrorDescriptor("catalog.outer_area.session.no_free_seats", ErrorKind.Conflict, "Session has no free seats."));
    }

    [Fact]
    public async Task GenericDeclaringType_DropsTypeParameters()
    {
        const string source = """
            using YAF.Core.Results;
            namespace Probe;
            public class Repository<T>
            {
                public sealed class Errors : IErrorCatalog
                {
                    public static readonly ErrorDescriptor NotFound = Error.NotFound("Item was not found.");
                }
            }
            """;

        using var result = await GeneratorHarness.RunAsync(source, [new ErrorCodeGenerator()], buildProperties: s_catalog);

        result.Diagnostics.Should().BeEmpty();
        Read(result, "Probe.Repository`1+Errors", "NotFound", typeof(int)).Code.Should().Be("catalog.repository.not_found");
    }

    [Fact]
    public async Task ErrorCodeAttribute_OverridesCode()
    {
        const string source = """
            using YAF.Core.Results;
            namespace Probe;
            public static class Session
            {
                public sealed class Errors : IErrorCatalog
                {
                    [ErrorCode("catalog.session.title_required")]
                    public static readonly ErrorDescriptor TitleMissing = Error.Validation("Title is required.");
                }
            }
            """;

        using var result = await GeneratorHarness.RunAsync(source, [new ErrorCodeGenerator()], buildProperties: s_catalog);

        result.Diagnostics.Should().BeEmpty();
        Read(result, "Probe.Session+Errors", "TitleMissing").Code.Should().Be("catalog.session.title_required");
    }

    [Fact]
    public async Task AcronymField_SplitsWords()
    {
        const string source = """
            using YAF.Core.Results;
            namespace Probe;
            public static class Session
            {
                public sealed class Errors : IErrorCatalog
                {
                    public static readonly ErrorDescriptor HTTPRequestFailed = Error.Unavailable("The request failed.");
                }
            }
            """;

        using var result = await GeneratorHarness.RunAsync(source, [new ErrorCodeGenerator()], buildProperties: s_catalog);

        result.Diagnostics.Should().BeEmpty();
        Read(result, "Probe.Session+Errors", "HTTPRequestFailed").Code.Should().Be("catalog.session.http_request_failed");
    }

    [Fact]
    public async Task MissingServiceCode_DefaultCode_FactoryThrows()
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

        using var result = await GeneratorHarness.RunAsync(source, [new ErrorCodeGenerator()]);

        var read = () => Read(result, "Probe.Session+Errors", "Full");
        read.Should().Throw<System.Reflection.TargetInvocationException>()
            .WithInnerException<TypeInitializationException>().WithInnerException<InvalidOperationException>();
    }

    [Fact]
    public async Task MethodBody_FactoryCall_IsNotIntercepted()
    {
        const string source = """
            using YAF.Core.Results;
            namespace Probe;
            public static class Handler
            {
                public static ErrorDescriptor Create() => Error.Failure("Inline.");
            }
            """;

        using var result = await GeneratorHarness.RunAsync(source, [new ErrorCodeGenerator()], buildProperties: s_catalog);

        var create = () => result.Assembly!.GetType("Probe.Handler")!.GetMethod("Create")!.Invoke(null, null);
        create.Should().Throw<System.Reflection.TargetInvocationException>().WithInnerException<InvalidOperationException>();
    }

    private static ErrorDescriptor Read(HarnessResult result, string typeName, string field, params Type[] typeArguments)
    {
        var type = result.Assembly!.GetType(typeName, throwOnError: true)!;
        var closed = typeArguments.Length == 0 ? type : type.MakeGenericType(typeArguments);
        return (ErrorDescriptor)closed.GetField(field)!.GetValue(null)!;
    }
}

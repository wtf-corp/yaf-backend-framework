using System.Globalization;
using Microsoft.CodeAnalysis;
using YAF.Analyzers.ErrorCodes;

namespace Analyzers.Tests.ErrorCodes;

public sealed class DuplicateCodeTests
{
    private static readonly Dictionary<string, string> s_catalog = new() { ["YafServiceCode"] = "catalog" };

    [Fact]
    public async Task ExplicitCodeEqualsDefaultCode_ReportsYAF3002OnBoth()
    {
        var source = """
            using YAF.Core.Results;
            namespace Probe;
            public static class Session
            {
                public sealed class Errors : IErrorCatalog
                {
                    public static readonly ErrorDescriptor Full = Error.Conflict("Session is full.");
                    [ErrorCode("catalog.session.full")]
                    public static readonly ErrorDescriptor NoSeats = Error.Conflict("No seats left.");
                }
            }
            """;

        using var result = await RunAsync(source);

        ShouldReportYaf3002(result, "Full", "Session.Errors.NoSeats", "NoSeats", "Session.Errors.Full");
    }

    [Fact]
    public async Task SamePathInDifferentNamespaces_ReportsYAF3002OnBoth()
    {
        var source = """
            using YAF.Core.Results;
            namespace Probe.Domain
            {
                public static class Session
                {
                    public sealed class Errors : IErrorCatalog
                    {
                        public static readonly ErrorDescriptor Full = Error.Conflict("Session is full.");
                    }
                }
            }
            namespace Probe.Application
            {
                public static class Session
                {
                    public sealed class Errors : IErrorCatalog
                    {
                        public static readonly ErrorDescriptor Full = Error.Conflict("Session is full.");
                    }
                }
            }
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().HaveCount(2).And.OnlyContain(diagnostic => diagnostic.Id == "YAF3002");
        result.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().BeEquivalentTo(
            "The code 'catalog.session.full' of error 'Probe.Domain.Session.Errors.Full' is also the code of 'Probe.Application.Session.Errors.Full'",
            "The code 'catalog.session.full' of error 'Probe.Application.Session.Errors.Full' is also the code of 'Probe.Domain.Session.Errors.Full'");
    }

    [Fact]
    public async Task SameFieldNameInDifferentTypes_ReportsNothing()
    {
        var source = """
            using YAF.Core.Results;
            namespace Probe;
            public static class Session
            {
                public sealed class Errors : IErrorCatalog
                {
                    public static readonly ErrorDescriptor Full = Error.Conflict("Session is full.");
                }
            }
            public static class Room
            {
                public sealed class Errors : IErrorCatalog
                {
                    public static readonly ErrorDescriptor Full = Error.Conflict("Room is full.");
                }
            }
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ConstructorCodeEqualsDefaultCode_ReportsYAF3002OnBoth()
    {
        var source = """
            using YAF.Core.Results;
            namespace Probe;
            public static class Session
            {
                public sealed class Errors : IErrorCatalog
                {
                    public static readonly ErrorDescriptor Full = Error.Conflict("Session is full.");
                    public static readonly ErrorDescriptor NoSeats = new("catalog.session.full", ErrorKind.Conflict, "No seats left.");
                }
            }
            """;

        using var result = await RunAsync(source);

        ShouldReportYaf3002(result, "Full", "Session.Errors.NoSeats", "NoSeats", "Session.Errors.Full");
    }

    [Fact]
    public async Task TwoConstructorFieldsSameCode_ReportsYAF3002OnBoth()
    {
        var source = """
            using YAF.Core.Results;
            namespace Probe;
            public static class Session
            {
                public sealed class Errors : IErrorCatalog
                {
                    public static readonly ErrorDescriptor Full = new ErrorDescriptor("catalog.session.full", ErrorKind.Conflict, "Full.");
                    public static readonly ErrorDescriptor NoSeats = new("catalog.session.full", ErrorKind.Conflict, "No seats left.");
                }
            }
            """;

        using var result = await RunAsync(source);

        ShouldReportYaf3002(result, "Full", "Session.Errors.NoSeats", "NoSeats", "Session.Errors.Full");
    }

    [Fact]
    public async Task ConstructorFieldsDistinctCodes_ReportsNothing()
    {
        var source = """
            using YAF.Core.Results;
            namespace Probe;
            public static class Session
            {
                public sealed class Errors : IErrorCatalog
                {
                    public static readonly ErrorDescriptor Full = Error.Conflict("Session is full.");
                    public static readonly ErrorDescriptor Closed = new("catalog.session.closed", ErrorKind.Conflict, "Closed.");
                    public static readonly ErrorDescriptor Gone = new ErrorDescriptor("catalog.session.gone", ErrorKind.NotFound, "Gone.");
                }
            }
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
    }

    private static Task<HarnessResult> RunAsync(string source) =>
        GeneratorHarness.RunAsync(source, [new ErrorCodeGenerator()], [new DuplicateCodeAnalyzer()], s_catalog);

    private static void ShouldReportYaf3002(HarnessResult result, string field, string other, string otherField, string otherOfOther)
    {
        result.Diagnostics.Should().HaveCount(2).And.OnlyContain(diagnostic =>
            diagnostic.Id == "YAF3002" && diagnostic.Severity == DiagnosticSeverity.Error);
        result.Diagnostics.Should().ContainSingle(diagnostic => Text(diagnostic) == field)
            .Which.GetMessage(CultureInfo.InvariantCulture).Should().Contain(other);
        result.Diagnostics.Should().ContainSingle(diagnostic => Text(diagnostic) == otherField)
            .Which.GetMessage(CultureInfo.InvariantCulture).Should().Contain(otherOfOther);
    }

    private static string Text(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.ToString().Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length);
}

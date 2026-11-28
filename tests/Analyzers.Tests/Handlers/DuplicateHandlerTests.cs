using System.Globalization;
using Microsoft.CodeAnalysis;
using YAF.Analyzers.Handlers;

namespace Analyzers.Tests.Handlers;

public sealed class DuplicateHandlerTests
{
    private const string Usings = """
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using YAF.Application;
        using YAF.Core.Results;

        """;

    [Fact]
    public async Task TwoHandlersForOneRequest_ReportsYAF4005()
    {
        const string source = Usings + """
            namespace Probe;
            public sealed record Close : ICommand<Result>;
            internal sealed class CloseHandler : IRequestHandler<Close, Result>
            {
                public ValueTask<Result> HandleAsync(Close request, CancellationToken ct) => ValueTask.FromResult(Result.Success());
            }
            internal sealed class OtherCloseHandler : IRequestHandler<Close, Result>
            {
                public ValueTask<Result> HandleAsync(Close request, CancellationToken ct) => ValueTask.FromResult(Result.Success());
            }
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().HaveCount(2).And.OnlyContain(diagnostic => diagnostic.Id == "YAF4005"
            && diagnostic.Severity == DiagnosticSeverity.Error);
        result.Diagnostics.Select(Text).Should().BeEquivalentTo("CloseHandler", "OtherCloseHandler");
        result.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().BeEquivalentTo(
            "The request 'Probe.Close' is handled by 'Probe.CloseHandler' and also by 'Probe.OtherCloseHandler'",
            "The request 'Probe.Close' is handled by 'Probe.OtherCloseHandler' and also by 'Probe.CloseHandler'");
    }

    [Fact]
    public async Task TwoHandlersForOneStreamQuery_ReportsYAF4005()
    {
        const string source = Usings + """
            namespace Probe;
            public sealed record Count : IStreamQuery<int>;
            internal sealed class CountHandler : IStreamQueryHandler<Count, int>
            {
                public IAsyncEnumerable<int> HandleAsync(Count query, CancellationToken ct) => System.Linq.AsyncEnumerable.Empty<int>();
            }
            internal sealed class OtherCountHandler : IStreamQueryHandler<Count, int>
            {
                public IAsyncEnumerable<int> HandleAsync(Count query, CancellationToken ct) => System.Linq.AsyncEnumerable.Empty<int>();
            }
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().HaveCount(2).And.OnlyContain(diagnostic => diagnostic.Id == "YAF4005");
        result.Diagnostics.Select(Text).Should().BeEquivalentTo("CountHandler", "OtherCountHandler");
    }

    [Fact]
    public async Task OneHandler_ReportsNothing()
    {
        const string source = Usings + """
            namespace Probe;
            public sealed record Close : ICommand<Result>;
            internal sealed class CloseHandler : IRequestHandler<Close, Result>
            {
                public ValueTask<Result> HandleAsync(Close request, CancellationToken ct) => ValueTask.FromResult(Result.Success());
            }
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task HandlersOfTwoRequests_ReportNothing()
    {
        const string source = Usings + """
            namespace Probe;
            public sealed record Open : ICommand<Result>;
            public sealed record Close : ICommand<Result>;
            internal sealed class OpenHandler : IRequestHandler<Open, Result>
            {
                public ValueTask<Result> HandleAsync(Open request, CancellationToken ct) => ValueTask.FromResult(Result.Success());
            }
            internal sealed class CloseHandler : IRequestHandler<Close, Result>
            {
                public ValueTask<Result> HandleAsync(Close request, CancellationToken ct) => ValueTask.FromResult(Result.Success());
            }
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task AbstractBaseOfTheHandler_ReportsNothing()
    {
        const string source = Usings + """
            namespace Probe;
            public sealed record Close : ICommand<Result>;
            internal abstract class CloseHandlerBase : IRequestHandler<Close, Result>
            {
                public ValueTask<Result> HandleAsync(Close request, CancellationToken ct) => ValueTask.FromResult(Result.Success());
            }
            internal sealed class CloseHandler : CloseHandlerBase;
            """;

        using var result = await RunAsync(source);

        result.Diagnostics.Should().BeEmpty();
    }

    private static Task<HarnessResult> RunAsync(string source) =>
        GeneratorHarness.RunAsync(source, analyzers: [new DuplicateHandlerAnalyzer()]);

    private static string Text(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.ToString().Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length);
}

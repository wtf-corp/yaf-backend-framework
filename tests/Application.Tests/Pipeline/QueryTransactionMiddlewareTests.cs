using YAF.Application.Pipeline;
using YAF.Application.UnitOfWork;
using YAF.Core.Results;

namespace Application.Tests.Pipeline;

public sealed class QueryTransactionMiddlewareTests
{
    private readonly List<string> _calls = [];
    private readonly RecordingQueryTransaction _transaction;

    public QueryTransactionMiddlewareTests()
    {
        _transaction = new RecordingQueryTransaction(_calls);
    }

    [Fact]
    public async Task Invoke_Query_RunsInsideTheTransaction()
    {
        var middleware = new QueryTransactionMiddleware<FindSpeakerQuery, Result<string>>(_transaction);

        var result = await middleware.InvokeAsync(
            new FindSpeakerQuery(),
            _ =>
            {
                _calls.Add("handler");
                return ValueTask.FromResult(Result.Success("Ada Example"));
            },
            TestContext.Current.CancellationToken);

        result.Value.Should().Be("Ada Example");
        _calls.Should().Equal("begin", "handler", "end");
        _transaction.Token.Should().Be(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Invoke_QueryFails_ReturnsTheResultOfTheTransaction()
    {
        var middleware = new QueryTransactionMiddleware<FindSpeakerQuery, Result<string>>(_transaction);

        var result = await middleware.InvokeAsync(
            new FindSpeakerQuery(),
            _ =>
            {
                _calls.Add("handler");
                return ValueTask.FromResult(Result<string>.Failure(SpeakerErrors.NotFound.ToError()));
            },
            TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle().Which.Code.Should().Be(SpeakerErrors.NotFound.Code);
        _calls.Should().Equal("begin", "handler", "end");
    }

    [Fact]
    public async Task Invoke_Command_IsNotWrapped()
    {
        var middleware = new QueryTransactionMiddleware<RenameSpeakerCommand, Result>(_transaction);

        var result = await middleware.InvokeAsync(
            new RenameSpeakerCommand("Ada Example"),
            _ =>
            {
                _calls.Add("handler");
                return ValueTask.FromResult(Result.Success());
            },
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _calls.Should().Equal("handler");
        _transaction.Executions.Should().Be(0);
    }

    /// <summary>A query transaction that records when it begins and ends the work.</summary>
    private sealed class RecordingQueryTransaction(List<string> calls) : IQueryTransaction
    {
        public int Executions { get; private set; }

        public CancellationToken Token { get; private set; }

        public async ValueTask<TResult> ExecuteAsync<TResult>(Func<CancellationToken, ValueTask<TResult>> work, CancellationToken ct)
            where TResult : IResult
        {
            Executions++;
            Token = ct;
            calls.Add("begin");
            var result = await work(ct).ConfigureAwait(false);
            calls.Add("end");
            return result;
        }

        public IAsyncEnumerable<TItem> Stream<TItem>(Func<CancellationToken, IAsyncEnumerable<TItem>> work, CancellationToken ct) =>
            throw new NotSupportedException("A request that is not a stream query is not streamed.");
    }
}

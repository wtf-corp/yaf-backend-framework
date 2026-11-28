using YAF.Application.Pipeline;
using YAF.Application.UnitOfWork;
using YAF.Core.Results;

namespace Application.Tests.Pipeline;

public sealed class UnitOfWorkMiddlewareTests
{
    private readonly List<string> _calls = [];
    private readonly RecordingUnitOfWork _unitOfWork;

    public UnitOfWorkMiddlewareTests()
    {
        _unitOfWork = new RecordingUnitOfWork(_calls);
    }

    [Fact]
    public async Task Invoke_Command_RunsInsideTheUnitOfWork()
    {
        var middleware = new UnitOfWorkMiddleware<RenameSpeakerCommand, Result>(_unitOfWork);

        var result = await middleware.InvokeAsync(
            new RenameSpeakerCommand("Ada Example"), Handler(Result.Success()), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _calls.Should().Equal("begin", "handler", "end");
        _unitOfWork.Token.Should().Be(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Invoke_CommandFails_ReturnsTheResultOfTheUnitOfWork()
    {
        var middleware = new UnitOfWorkMiddleware<RenameSpeakerCommand, Result>(_unitOfWork);

        var result = await middleware.InvokeAsync(
            new RenameSpeakerCommand("Ada Example"),
            Handler(Result.Failure(SpeakerErrors.NotFound.ToError())),
            TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle().Which.Code.Should().Be(SpeakerErrors.NotFound.Code);
        _calls.Should().Equal("begin", "handler", "end");
    }

    [Fact]
    public async Task Invoke_Query_IsNotWrapped()
    {
        var middleware = new UnitOfWorkMiddleware<FindSpeakerQuery, Result<string>>(_unitOfWork);

        var result = await middleware.InvokeAsync(
            new FindSpeakerQuery(),
            _ =>
            {
                _calls.Add("handler");
                return ValueTask.FromResult(Result.Success("Ada Example"));
            },
            TestContext.Current.CancellationToken);

        result.Value.Should().Be("Ada Example");
        _calls.Should().Equal("handler");
        _unitOfWork.Executions.Should().Be(0);
    }

    private RequestDelegate<Result> Handler(Result result) => _ =>
    {
        _calls.Add("handler");
        return ValueTask.FromResult(result);
    };

    /// <summary>A unit of work that records when it begins and ends the work.</summary>
    private sealed class RecordingUnitOfWork(List<string> calls) : IUnitOfWork
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
    }
}

using Microsoft.Extensions.Logging;
using YAF.Application.Pipeline;
using YAF.Application.UnitOfWork;
using YAF.Core.Results;

namespace Application.Tests.Pipeline;

public sealed class ExceptionBoundaryTests
{
    private readonly RecordingLogger<ExceptionBoundaryMiddleware<RenameSpeakerCommand, Result>> _log = new();

    [Fact]
    public async Task Invoke_ConcurrencyConflict_FailsWithConflictCode()
    {
        var result = await InvokeAsync(_ => throw new ConcurrencyConflictException());

        var error = result.Errors.Should().ContainSingle().Subject;
        error.Code.Should().Be("core.concurrency.conflict");
        error.Kind.Should().Be(ErrorKind.Conflict);
        _log.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task Invoke_DependencyUnavailable_FailsWithUnavailableCode()
    {
        var query = new ExceptionBoundaryMiddleware<FindSpeakerQuery, Result<string>>(
            new RecordingLogger<ExceptionBoundaryMiddleware<FindSpeakerQuery, Result<string>>>());

        var result = await query.InvokeAsync(
            new FindSpeakerQuery(), _ => throw new DependencyUnavailableException("down"), TestContext.Current.CancellationToken);

        var error = result.Errors.Should().ContainSingle().Subject;
        error.Code.Should().Be("core.dependency.unavailable");
        error.Kind.Should().Be(ErrorKind.Unavailable);
    }

    [Fact]
    public async Task Invoke_Cancelled_Rethrows()
    {
        var cancelled = new OperationCanceledException();

        var act = () => InvokeAsync(_ => throw cancelled).AsTask();

        (await act.Should().ThrowAsync<OperationCanceledException>()).Which.Should().BeSameAs(cancelled);
        _log.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task Invoke_OtherException_LogsOnceAndRethrows()
    {
        var thrown = new InvalidOperationException("Ada Example");

        var act = () => InvokeAsync(_ => throw thrown).AsTask();

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(thrown);
        var record = _log.Records.Should().ContainSingle().Subject;
        (record.Id.Id, record.Id.Name, record.Level).Should().Be((1004, "Unhandled", LogLevel.Error));
        record.Message.Should().Be("Unhandled InvalidOperationException in RenameSpeakerCommand");
    }

    [Fact]
    public async Task Invoke_Success_ReturnsTheResultUnchanged()
    {
        var result = await InvokeAsync(_ => ValueTask.FromResult(Result.Success()));

        result.IsSuccess.Should().BeTrue();
        _log.Records.Should().BeEmpty();
    }

    private ValueTask<Result> InvokeAsync(RequestDelegate<Result> next) =>
        new ExceptionBoundaryMiddleware<RenameSpeakerCommand, Result>(_log)
            .InvokeAsync(new RenameSpeakerCommand("Ada Example"), next, TestContext.Current.CancellationToken);
}

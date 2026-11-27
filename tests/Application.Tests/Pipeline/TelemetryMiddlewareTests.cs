using System.Diagnostics;
using Microsoft.Extensions.Logging;
using YAF.Application.Pipeline;
using YAF.Core.Results;

namespace Application.Tests.Pipeline;

public sealed class TelemetryMiddlewareTests : IDisposable
{
    private const string SecretName = "Ada Example";

    private readonly TelemetryCapture _capture = new();
    private readonly RecordingLogger<TelemetryMiddleware<RenameSpeakerCommand, Result>> _log = new();

    [Fact]
    public async Task Invoke_StartsTheRequestActivity()
    {
        Activity? current = null;

        await RenameAsync(_ =>
        {
            current = Activity.Current;
            return ValueTask.FromResult(Result.Success());
        });

        var activity = _capture.Of(nameof(RenameSpeakerCommand)).Should().ContainSingle().Subject;
        current.Should().BeSameAs(activity);
        activity.OperationName.Should().Be("yaf.request");
        activity.Kind.Should().Be(ActivityKind.Internal);
        activity.GetTagItem("yaf.request.kind").Should().Be("command");
        activity.GetTagItem("yaf.request.outcome").Should().Be("success");
        activity.GetTagItem("yaf.error.codes").Should().BeNull();
        activity.Status.Should().Be(ActivityStatusCode.Unset);
        _log.Records.Select(record => (record.Id.Id, record.Id.Name, record.Level, record.Message)).Should().Equal(
            (1001, "Handling", LogLevel.Debug, "Handling RenameSpeakerCommand"),
            (1002, "Handled", LogLevel.Debug, "Handled RenameSpeakerCommand"));
    }

    [Fact]
    public async Task Invoke_RecordsDurationWithOutcome()
    {
        var query = new TelemetryMiddleware<FindSpeakerQuery, Result<string>>(
            new RecordingLogger<TelemetryMiddleware<FindSpeakerQuery, Result<string>>>(), _capture);

        await query.InvokeAsync(
            new FindSpeakerQuery(), _ => ValueTask.FromResult<Result<string>>(SpeakerErrors.NotFound.ToError()), TestContext.Current.CancellationToken);

        _capture.Unit.Should().Be("s");
        var (value, tags) = _capture.Durations.Should().ContainSingle().Subject;
        value.Should().BeGreaterThanOrEqualTo(0);
        tags.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["yaf.request.name"] = nameof(FindSpeakerQuery),
            ["yaf.request.kind"] = "query",
            ["yaf.request.outcome"] = "not_found",
        });
    }

    [Fact]
    public async Task Invoke_Failure_LogsCodesOnly()
    {
        var codes = $"{SpeakerErrors.Unreachable.Code},{SpeakerErrors.NotFound.Code}";

        await RenameAsync(_ => ValueTask.FromResult(
            Result.Failure([SpeakerErrors.Unreachable.With(("Name", SecretName)), SpeakerErrors.NotFound.ToError()])));

        _log.Records.Select(record => record.Id.Id).Should().Equal(1001, 1003);
        _log.Records[1].Level.Should().Be(LogLevel.Error);
        _log.Records[1].Message.Should().Be($"Failed RenameSpeakerCommand with {codes}");
        _log.Records.Should().NotContain(record => record.Message.Contains(SecretName, StringComparison.Ordinal)
            || record.State.Any(value => (value.Value as string ?? string.Empty).Contains(SecretName, StringComparison.Ordinal)));
        var activity = _capture.Of(nameof(RenameSpeakerCommand)).Should().ContainSingle().Subject;
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.GetTagItem("yaf.request.outcome").Should().Be("failure");
        activity.GetTagItem("yaf.error.codes").Should().Be(codes);
        activity.Tags.Should().NotContain(tag => (tag.Value ?? string.Empty).Contains(SecretName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invoke_ValidationFailure_LeavesStatusUnset()
    {
        await RenameAsync(_ => ValueTask.FromResult<Result>(SpeakerErrors.NameRequired.ToError() with { Target = "NewName" }));

        var activity = _capture.Of(nameof(RenameSpeakerCommand)).Should().ContainSingle().Subject;
        activity.Status.Should().Be(ActivityStatusCode.Unset);
        activity.GetTagItem("yaf.request.outcome").Should().Be("validation");
        activity.GetTagItem("yaf.error.codes").Should().Be(SpeakerErrors.NameRequired.Code);
        _log.Records[^1].Should().Match<LogRecord>(record => record.Id.Id == 1003 && record.Level == LogLevel.Debug);
    }

    [Fact]
    public async Task Invoke_UnavailableResult_SetsErrorStatus()
    {
        await RenameAsync(_ => ValueTask.FromResult<Result>(SpeakerErrors.DirectoryDown.ToError()));

        _capture.Of(nameof(RenameSpeakerCommand)).Should().ContainSingle().Which.Status.Should().Be(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task Invoke_Exception_SetsErrorStatusAndRethrows()
    {
        var thrown = new InvalidOperationException("The store went away.");

        var invoke = () => RenameAsync(_ => throw thrown);

        (await invoke.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(thrown);
        var activity = _capture.Of(nameof(RenameSpeakerCommand)).Should().ContainSingle().Subject;
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.GetTagItem("yaf.request.outcome").Should().Be("exception");
        _capture.Durations.Should().ContainSingle().Which.Tags.Should().Contain(new KeyValuePair<string, object?>("yaf.request.outcome", "exception"));
        _log.Records.Select(record => record.Id.Id).Should().Equal(1001);
    }

    public void Dispose() => _capture.Dispose();

    private Task<Result> RenameAsync(RequestDelegate<Result> handler) =>
        new TelemetryMiddleware<RenameSpeakerCommand, Result>(_log, _capture)
            .InvokeAsync(new RenameSpeakerCommand(SecretName), handler, TestContext.Current.CancellationToken).AsTask();
}

using YAF.Core.Results;

namespace Core.Tests.Results;

public sealed class ResultAsyncTests
{
    private static readonly ErrorDescriptor s_full = new("test.session.full", ErrorKind.Conflict, "Session is full.");
    private static readonly ErrorDescriptor s_closed = new("test.session.closed", ErrorKind.Conflict, "Session is closed.");

    [Fact]
    public async Task MapAsync_AsyncLambda_ResolvesAndMaps()
    {
        Result<int> result = 42;

        var mapped = await ExpectTask(result.MapAsync(async value => { await Task.Yield(); return value + 1; })).ConfigureAwait(true);

        mapped.Value.Should().Be(43);
    }

    [Fact]
    public async Task MapAsync_ValueTaskDelegate_ResolvesAndMaps()
    {
        Result<int> result = 42;

        var mapped = await ExpectValueTask(result.MapAsync(value => ValueTask.FromResult(value + 1))).ConfigureAwait(true);

        mapped.Value.Should().Be(43);
    }

    [Fact]
    public async Task MapAsync_OnFailure_KeepsErrorsAndSkipsSelector()
    {
        Result<int> result = new[] { s_full.ToError(), s_closed.ToError() };
        var called = false;

        var mapped = await result.MapAsync(async value => { await Task.Yield(); called = true; return value + 1; }).ConfigureAwait(true);

        mapped.Errors.Select(error => error.Code).Should().Equal(s_full.Code, s_closed.Code);
        called.Should().BeFalse();
    }

    [Fact]
    public async Task MapAsync_ValueTaskOnDefault_KeepsUninitialised()
    {
        var result = default(Result<int>);

        var mapped = await result.MapAsync(value => ValueTask.FromResult(value + 1)).ConfigureAwait(true);

        mapped.Errors.Should().ContainSingle().Which.Code.Should().Be(CoreErrors.Uninitialised.Code);
    }

    [Fact]
    public void MapAsync_NullSelector_Throws()
    {
        Result<int> result = 42;

        Action act = () => result.MapAsync((Func<int, Task<int>>)null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("selector");
    }

    [Fact]
    public async Task ThenAsync_AsyncLambda_ReturnsResultOfContinuation()
    {
        Result<int> result = 42;

        var next = await ExpectTask(result.ThenAsync(async value => { await Task.Yield(); return Result.Success(value * 2); })).ConfigureAwait(true);

        next.Value.Should().Be(84);
    }

    [Fact]
    public async Task ThenAsync_OnFailure_KeepsErrorsAndSkipsContinuation()
    {
        Result<int> result = s_full;
        var called = false;

        var next = await result.ThenAsync(async value => { await Task.Yield(); called = true; return Result.Success(value * 2); }).ConfigureAwait(true);

        next.Errors.Should().ContainSingle().Which.Code.Should().Be(s_full.Code);
        called.Should().BeFalse();
    }

    [Fact]
    public async Task ThenAsync_ValueTaskDelegate_ReturnsItsErrors()
    {
        Result<int> result = 42;

        var next = await ExpectValueTask(result.ThenAsync(_ => ValueTask.FromResult<Result<string>>(s_closed))).ConfigureAwait(true);

        next.Errors.Should().ContainSingle().Which.Code.Should().Be(s_closed.Code);
    }

    [Fact]
    public async Task ThenAsync_ValueTaskOnFailure_KeepsErrors()
    {
        Result<int> result = s_full;

        var next = await result.ThenAsync(value => ValueTask.FromResult(Result.Success(value * 2))).ConfigureAwait(true);

        next.Errors.Should().ContainSingle().Which.Code.Should().Be(s_full.Code);
    }

    [Fact]
    public void ThenAsync_NullContinuation_Throws()
    {
        Result<int> result = 42;

        Action act = () => result.ThenAsync((Func<int, Task<Result<int>>>)null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("continuation");
    }

    [Fact]
    public async Task MatchAsync_AsyncLambdasOnSuccess_CallsOnSuccessWithValue()
    {
        Result<int> result = 42;

        var text = await ExpectTask(result.MatchAsync(
            async value => { await Task.Yield(); return $"value {value}"; },
            async errors => { await Task.Yield(); return $"errors {errors.Count}"; })).ConfigureAwait(true);

        text.Should().Be("value 42");
    }

    [Fact]
    public async Task MatchAsync_AsyncLambdasOnFailure_CallsOnFailureWithErrors()
    {
        Result<int> result = new[] { s_full.ToError(), s_closed.ToError() };

        var text = await result.MatchAsync(
            async value => { await Task.Yield(); return $"value {value}"; },
            async errors => { await Task.Yield(); return $"errors {errors.Count}"; }).ConfigureAwait(true);

        text.Should().Be("errors 2");
    }

    [Fact]
    public async Task MatchAsync_ValueTaskDelegatesOnSuccess_CallsOnSuccessWithValue()
    {
        Result<int> result = 42;

        var text = await ExpectValueTask(result.MatchAsync(
            value => ValueTask.FromResult($"value {value}"),
            errors => ValueTask.FromResult(errors[0].Code))).ConfigureAwait(true);

        text.Should().Be("value 42");
    }

    [Fact]
    public async Task MatchAsync_ValueTaskDelegatesOnDefault_CallsOnFailureWithUninitialised()
    {
        var result = default(Result<int>);

        var code = await result.MatchAsync(
            value => ValueTask.FromResult($"value {value}"),
            errors => ValueTask.FromResult(errors[0].Code)).ConfigureAwait(true);

        code.Should().Be(CoreErrors.Uninitialised.Code);
    }

    [Fact]
    public void MatchAsync_NullFunction_Throws()
    {
        Result<int> result = 42;

        Action act = () => result.MatchAsync(Task.FromResult, (Func<IReadOnlyList<Error>, Task<int>>)null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("onFailure");
    }

    [Fact]
    public async Task SwitchAsync_AsyncLambdasOnSuccess_RunsOnSuccessWithValue()
    {
        Result<int> result = 42;
        var seen = new List<string>();

        await ExpectTask(result.SwitchAsync(
            async value => { await Task.Yield(); seen.Add($"value {value}"); },
            async errors => { await Task.Yield(); seen.Add($"errors {errors.Count}"); })).ConfigureAwait(true);

        seen.Should().Equal("value 42");
    }

    [Fact]
    public async Task SwitchAsync_AsyncLambdasOnFailure_RunsOnFailureWithErrors()
    {
        Result<int> result = new[] { s_full.ToError(), s_closed.ToError() };
        var seen = new List<string>();

        await result.SwitchAsync(
            async value => { await Task.Yield(); seen.Add($"value {value}"); },
            async errors => { await Task.Yield(); seen.Add($"errors {errors.Count}"); }).ConfigureAwait(true);

        seen.Should().Equal("errors 2");
    }

    [Fact]
    public async Task SwitchAsync_ValueTaskDelegatesOnSuccess_RunsOnSuccessWithValue()
    {
        Result<int> result = 42;
        var seen = new List<string>();

        await ExpectValueTask(result.SwitchAsync(
            value => Record(seen, $"value {value}"),
            errors => Record(seen, $"errors {errors.Count}"))).ConfigureAwait(true);

        seen.Should().Equal("value 42");
    }

    [Fact]
    public async Task SwitchAsync_ValueTaskDelegatesOnFailure_RunsOnFailureWithErrors()
    {
        Result<int> result = s_full;
        var seen = new List<string>();

        await result.SwitchAsync(
            value => Record(seen, $"value {value}"),
            errors => Record(seen, $"errors {errors[0].Code}")).ConfigureAwait(true);

        seen.Should().Equal($"errors {s_full.Code}");
    }

    [Fact]
    public void SwitchAsync_NullFunction_Throws()
    {
        Result<int> result = 42;

        Action act = () => result.SwitchAsync(null!, (Func<IReadOnlyList<Error>, Task>)(_ => Task.CompletedTask));

        act.Should().Throw<ArgumentNullException>().WithParameterName("onSuccess");
    }

    private static Task<TResult> ExpectTask<TResult>(Task<TResult> pending) => pending;

    private static ValueTask<TResult> ExpectValueTask<TResult>(ValueTask<TResult> pending) => pending;

    private static Task ExpectTask(Task pending) => pending;

    private static ValueTask ExpectValueTask(ValueTask pending) => pending;

    private static ValueTask Record(List<string> seen, string entry)
    {
        seen.Add(entry);
        return ValueTask.CompletedTask;
    }
}

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

    #region then-async
    [Fact]
    public async Task ThenAsync_ValueTaskOnFailure_KeepsErrors()
    {
        Result<int> result = s_full;

        var next = await result.ThenAsync(value => ValueTask.FromResult(Result.Success(value * 2))).ConfigureAwait(true);

        next.Errors.Should().ContainSingle().Which.Code.Should().Be(s_full.Code);
    }
    #endregion

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

    [Fact]
    public async Task EnsureAsync_PredicateFalse_FailsWithDescriptor()
    {
        Result<int> result = 42;

        var ensured = await ExpectTask(result.EnsureAsync(async value => { await Task.Yield(); return value > 100; }, s_full)).ConfigureAwait(true);

        ensured.Errors.Should().ContainSingle().Which.Code.Should().Be(s_full.Code);
    }

    [Fact]
    public async Task EnsureAsync_ValueTaskPredicateTrue_KeepsValue()
    {
        Result<int> result = 42;

        var ensured = await ExpectValueTask(result.EnsureAsync(value => ValueTask.FromResult(value > 0), s_full)).ConfigureAwait(true);

        ensured.Value.Should().Be(42);
    }

    [Fact]
    public async Task EnsureAsync_OnFailure_KeepsErrorsAndSkipsPredicate()
    {
        Result<int> result = s_closed;
        var called = false;

        var ensured = await result.EnsureAsync(async _ => { await Task.Yield(); called = true; return false; }, s_full).ConfigureAwait(true);

        ensured.Errors.Should().ContainSingle().Which.Code.Should().Be(s_closed.Code);
        called.Should().BeFalse();
    }

    [Fact]
    public void EnsureAsync_NullError_Throws()
    {
        Result<int> result = 42;

        Action act = () => result.EnsureAsync(_ => Task.FromResult(true), null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("error");
    }

    [Fact]
    public async Task TapAsync_AsyncLambdaOnSuccess_RunsActionAndKeepsValue()
    {
        Result<int> result = 42;
        var seen = new List<string>();

        var tapped = await ExpectTask(result.TapAsync(async value => { await Task.Yield(); seen.Add($"value {value}"); })).ConfigureAwait(true);

        tapped.Value.Should().Be(42);
        seen.Should().Equal("value 42");
    }

    [Fact]
    public async Task TapAsync_ValueTaskOnFailure_KeepsErrorsAndSkipsAction()
    {
        Result<int> result = s_full;
        var seen = new List<string>();

        var tapped = await ExpectValueTask(result.TapAsync(value => Record(seen, $"value {value}"))).ConfigureAwait(true);

        tapped.Errors.Should().ContainSingle().Which.Code.Should().Be(s_full.Code);
        seen.Should().BeEmpty();
    }

    [Fact]
    public void TapAsync_NullAction_Throws()
    {
        Result<int> result = 42;

        Action act = () => result.TapAsync((Func<int, Task>)null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("action");
    }

    [Fact]
    public async Task ElseAsync_AsyncLambdaOnFailure_ReturnsFallbackFromErrors()
    {
        Result<int> result = new[] { s_full.ToError(), s_closed.ToError() };

        var recovered = await ExpectTask(result.ElseAsync(async errors => { await Task.Yield(); return errors.Count; })).ConfigureAwait(true);

        recovered.Value.Should().Be(2);
    }

    [Fact]
    public async Task ElseAsync_ValueTaskOnSuccess_KeepsValueAndSkipsFallback()
    {
        Result<int> result = 42;
        var called = false;

        var recovered = await ExpectValueTask(result.ElseAsync(_ => { called = true; return ValueTask.FromResult(0); })).ConfigureAwait(true);

        recovered.Value.Should().Be(42);
        called.Should().BeFalse();
    }

    [Fact]
    public async Task ElseAsync_ValueTaskOnDefault_PassesUninitialised()
    {
        var result = default(Result<string>);

        var recovered = await result.ElseAsync(errors => ValueTask.FromResult(errors[0].Code)).ConfigureAwait(true);

        recovered.Value.Should().Be(CoreErrors.Uninitialised.Code);
    }

    [Fact]
    public void ElseAsync_NullFallback_Throws()
    {
        Result<int> result = 42;

        Action act = () => result.ElseAsync((Func<IReadOnlyList<Error>, Task<int>>)null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("fallback");
    }

    [Fact]
    public async Task ResultThenAsync_AsyncActionOnSuccess_RunsActionAndSucceeds()
    {
        var seen = new List<string>();

        var next = await ExpectTask(Result.Success().ThenAsync(async () => { await Task.Yield(); seen.Add("ran"); })).ConfigureAwait(true);

        next.IsSuccess.Should().BeTrue();
        seen.Should().Equal("ran");
    }

    [Fact]
    public async Task ResultThenAsync_ValueTaskActionOnFailure_KeepsErrorsAndSkipsAction()
    {
        Result result = s_full;
        var seen = new List<string>();

        var next = await ExpectValueTask(result.ThenAsync(() => Record(seen, "ran"))).ConfigureAwait(true);

        next.Errors.Should().ContainSingle().Which.Code.Should().Be(s_full.Code);
        seen.Should().BeEmpty();
    }

    [Fact]
    public async Task ResultThenAsync_AsyncContinuation_ReturnsItsErrors()
    {
        var next = await ExpectTask(Result.Success().ThenAsync(async () => { await Task.Yield(); return (Result)s_closed; })).ConfigureAwait(true);

        next.Errors.Should().ContainSingle().Which.Code.Should().Be(s_closed.Code);
    }

    [Fact]
    public async Task ResultThenAsync_ValueTaskContinuationOnDefault_KeepsUninitialised()
    {
        var called = false;

        var next = await ExpectValueTask(default(Result).ThenAsync(() => { called = true; return ValueTask.FromResult(Result.Success()); })).ConfigureAwait(true);

        next.Errors.Should().ContainSingle().Which.Code.Should().Be(CoreErrors.Uninitialised.Code);
        called.Should().BeFalse();
    }

    [Fact]
    public async Task ResultThenAsync_AsyncValueContinuation_ReturnsItsValue()
    {
        var next = await ExpectTask(Result.Success().ThenAsync(async () => { await Task.Yield(); return Result.Success(42); })).ConfigureAwait(true);

        next.Value.Should().Be(42);
    }

    [Fact]
    public async Task ResultThenAsync_ValueTaskValueContinuationOnFailure_KeepsErrors()
    {
        Result result = new[] { s_full.ToError(), s_closed.ToError() };

        var next = await ExpectValueTask(result.ThenAsync(() => ValueTask.FromResult(Result.Success(42)))).ConfigureAwait(true);

        next.Errors.Select(error => error.Code).Should().Equal(s_full.Code, s_closed.Code);
    }

    [Fact]
    public void ResultThenAsync_NullContinuation_Throws()
    {
        Action act = () => Result.Success().ThenAsync((Func<Task<Result<int>>>)null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("continuation");
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

using System.Globalization;
using YAF.Core.Results;

namespace Core.Tests.Results;

public sealed class ResultCombinatorTests
{
    private static readonly ErrorDescriptor s_full = new("test.session.full", ErrorKind.Conflict, "Session is full.");
    private static readonly ErrorDescriptor s_closed = new("test.session.closed", ErrorKind.Conflict, "Session is closed.");

    [Fact]
    public void Match_OnSuccess_CallsOnSuccessWithValue()
    {
        Result<int> result = 42;

        var text = result.Match(value => $"value {value}", errors => $"errors {errors.Count}");

        text.Should().Be("value 42");
    }

    [Fact]
    public void Match_OnFailure_CallsOnFailureWithErrors()
    {
        Result<int> result = new[] { s_full.ToError(), s_closed.ToError() };

        var codes = result.Match(_ => [], errors => errors.Select(error => error.Code).ToArray());

        codes.Should().Equal(s_full.Code, s_closed.Code);
    }

    [Fact]
    public void Match_OnDefault_CallsOnFailureWithUninitialised()
    {
        var result = default(Result<int>);

        var code = result.Match(_ => "success", errors => errors[0].Code);

        code.Should().Be(CoreErrors.Uninitialised.Code);
    }

    [Fact]
    public void Match_NullFunction_Throws()
    {
        Result<int> result = 42;

        var act = () => result.Match(value => value, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("onFailure");
    }

    [Fact]
    public void Switch_OnSuccess_RunsOnSuccessWithValue()
    {
        Result<int> result = 42;
        var seen = new List<string>();

        result.Switch(value => seen.Add($"value {value}"), errors => seen.Add($"errors {errors.Count}"));

        seen.Should().Equal("value 42");
    }

    [Fact]
    public void Switch_OnFailure_RunsOnFailureWithErrors()
    {
        Result<int> result = s_full;
        var seen = new List<string>();

        result.Switch(value => seen.Add($"value {value}"), errors => seen.Add(errors[0].Code));

        seen.Should().Equal(s_full.Code);
    }

    [Fact]
    public void Map_OnSuccess_TransformsValue()
    {
        Result<int> result = 42;

        var mapped = result.Map(value => value.ToString(CultureInfo.InvariantCulture));

        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be("42");
    }

    [Fact]
    public void Map_OnFailure_KeepsErrors()
    {
        Result<int> result = s_full;
        var called = false;

        var mapped = result.Map(value =>
        {
            called = true;
            return value.ToString(CultureInfo.InvariantCulture);
        });

        mapped.IsFailure.Should().BeTrue();
        mapped.Errors.Should().ContainSingle().Which.Code.Should().Be(s_full.Code);
        called.Should().BeFalse();
    }

    [Fact]
    public void Map_OnDefault_KeepsUninitialised()
    {
        var result = default(Result<int>);

        var mapped = result.Map(value => value + 1);

        mapped.Errors.Should().ContainSingle().Which.Code.Should().Be(CoreErrors.Uninitialised.Code);
    }

    #region then
    [Fact]
    public void Then_OnSuccess_ReturnsResultOfContinuation()
    {
        Result<int> result = 42;

        var next = result.Then(value => Result.Success(value * 2));

        next.Value.Should().Be(84);
    }
    #endregion

    [Fact]
    public void Then_OnSuccessAndFailingContinuation_ReturnsItsErrors()
    {
        Result<int> result = 42;

        var next = result.Then<string>(_ => s_closed);

        next.Errors.Should().ContainSingle().Which.Code.Should().Be(s_closed.Code);
    }

    [Fact]
    public void Then_OnFailure_KeepsErrorsAndSkipsContinuation()
    {
        Result<int> result = new[] { s_full.ToError(), s_closed.ToError() };
        var called = false;

        var next = result.Then(value =>
        {
            called = true;
            return Result.Success(value.ToString(CultureInfo.InvariantCulture));
        });

        next.Errors.Select(error => error.Code).Should().Equal(s_full.Code, s_closed.Code);
        called.Should().BeFalse();
    }

    [Fact]
    public void ThenAction_OnSuccess_RunsActionAndStaysSuccess()
    {
        var result = Result.Success();
        var called = false;

        var next = result.Then(() => { called = true; });

        next.IsSuccess.Should().BeTrue();
        called.Should().BeTrue();
    }

    [Fact]
    public void ThenAction_OnFailure_KeepsErrorsAndSkipsAction()
    {
        Result result = s_full;
        var called = false;

        var next = result.Then(() => { called = true; });

        next.Errors.Should().ContainSingle().Which.Code.Should().Be(s_full.Code);
        called.Should().BeFalse();
    }

    [Fact]
    public void ThenResult_OnSuccess_ReturnsResultOfContinuation()
    {
        var result = Result.Success();

        var next = result.Then(() => (Result)s_closed);

        next.Errors.Should().ContainSingle().Which.Code.Should().Be(s_closed.Code);
    }

    [Fact]
    public void ThenResult_OnFailure_KeepsErrorsAndSkipsContinuation()
    {
        Result result = s_full;
        var called = false;

        var next = result.Then(() =>
        {
            called = true;
            return Result.Success();
        });

        next.Errors.Should().ContainSingle().Which.Code.Should().Be(s_full.Code);
        called.Should().BeFalse();
    }

    [Fact]
    public void ThenValue_OnSuccess_ReturnsResultOfContinuation()
    {
        var result = Result.Success();

        var next = result.Then(() => Result.Success(42));

        next.Value.Should().Be(42);
    }

    [Fact]
    public void ThenValue_OnFailure_KeepsErrorsAndSkipsContinuation()
    {
        Result result = s_full;
        var called = false;

        var next = result.Then(() =>
        {
            called = true;
            return Result.Success(42);
        });

        next.Errors.Should().ContainSingle().Which.Code.Should().Be(s_full.Code);
        called.Should().BeFalse();
    }

    [Fact]
    public void ThenValue_OnDefault_KeepsUninitialised()
    {
        var result = default(Result);

        var next = result.Then(() => Result.Success(42));

        next.Errors.Should().ContainSingle().Which.Code.Should().Be(CoreErrors.Uninitialised.Code);
    }

    [Fact]
    public void Ensure_OnSuccessAndPredicateTrue_KeepsValue()
    {
        Result<int> result = 42;

        var ensured = result.Ensure(value => value > 0, s_full);

        ensured.Value.Should().Be(42);
    }

    #region ensure
    [Fact]
    public void Ensure_OnSuccessAndPredicateFalse_FailsWithDescriptor()
    {
        Result<int> result = 42;

        var ensured = result.Ensure(value => value < 0, s_full);

        ensured.Errors.Should().ContainSingle().Which.Code.Should().Be(s_full.Code);
    }
    #endregion

    [Fact]
    public void Ensure_OnFailure_KeepsErrorsAndSkipsPredicate()
    {
        Result<int> result = s_closed;
        var called = false;

        var ensured = result.Ensure(_ => called = true, s_full);

        ensured.Errors.Should().ContainSingle().Which.Code.Should().Be(s_closed.Code);
        called.Should().BeFalse();
    }

    [Fact]
    public void Ensure_NullDescriptor_Throws()
    {
        Result<int> result = 42;

        var act = () => result.Ensure(_ => true, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("error");
    }

    [Fact]
    public void Tap_OnSuccess_RunsActionAndKeepsValue()
    {
        Result<int> result = 42;
        var seen = new List<int>();

        var tapped = result.Tap(seen.Add);

        tapped.Value.Should().Be(42);
        seen.Should().Equal(42);
    }

    [Fact]
    public void Tap_OnFailure_KeepsErrorsAndSkipsAction()
    {
        Result<int> result = s_full;
        var seen = new List<int>();

        var tapped = result.Tap(seen.Add);

        tapped.Errors.Should().ContainSingle().Which.Code.Should().Be(s_full.Code);
        seen.Should().BeEmpty();
    }

    [Fact]
    public void Else_OnSuccess_KeepsValueAndSkipsFallback()
    {
        Result<int> result = 42;
        var called = false;

        var value = result.Else(_ =>
        {
            called = true;
            return -1;
        });

        value.Value.Should().Be(42);
        called.Should().BeFalse();
    }

    [Fact]
    public void Else_OnFailure_SucceedsWithFallbackFromErrors()
    {
        Result<int> result = new[] { s_full.ToError(), s_closed.ToError() };

        var value = result.Else(errors => errors.Count);

        value.IsSuccess.Should().BeTrue();
        value.Value.Should().Be(2);
    }

    [Fact]
    public void Else_OnDefault_PassesUninitialised()
    {
        var result = default(Result<string>);

        var value = result.Else(errors => errors[0].Code);

        value.Value.Should().Be(CoreErrors.Uninitialised.Code);
    }

    [Fact]
    public void Combine_AllSucceed_IsSuccess()
    {
        var combined = Result.Combine(Result.Success(), Result.Success());

        combined.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Combine_None_IsSuccess()
    {
        var combined = Result.Combine();

        combined.IsSuccess.Should().BeTrue();
    }

    #region combine
    [Fact]
    public void Combine_TwoFailures_KeepsAllErrorsInOrder()
    {
        Result first = new[] { s_full.ToError(), s_closed.ToError() };
        Result second = s_closed;

        var combined = Result.Combine(first, Result.Success(), second);

        combined.Errors.Select(error => error.Code).Should().Equal(s_full.Code, s_closed.Code, s_closed.Code);
    }
    #endregion

    [Fact]
    public void Combine_WithDefault_ReportsUninitialised()
    {
        var combined = Result.Combine(Result.Success(), default);

        combined.Errors.Should().ContainSingle().Which.Code.Should().Be(CoreErrors.Uninitialised.Code);
    }
}

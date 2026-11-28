using YAF.Application.Pipeline;
using YAF.Application.Validation;
using YAF.Core.Results;

namespace Application.Tests.Pipeline;

public sealed class ValidationMiddlewareTests
{
    private int _handlerCalls;

    [Fact]
    public async Task Invoke_TwoValidatorsFail_ReturnsTheirCombinedErrors()
    {
        var result = await InvokeAsync(
            new FixedValidator(SpeakerErrors.NameRequired.At("newName")),
            new FixedValidator(SpeakerErrors.NotFound.At("speakerId"), SpeakerErrors.Unreachable.ToError()));

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => (e.Code, e.Target)).Should().Equal(
            (SpeakerErrors.NameRequired.Code, "newName"),
            (SpeakerErrors.NotFound.Code, "speakerId"),
            (SpeakerErrors.Unreachable.Code, null));
    }

    [Fact]
    public async Task Invoke_NoValidator_CallsNext()
    {
        var result = await InvokeAsync();

        result.IsSuccess.Should().BeTrue();
        _handlerCalls.Should().Be(1);
    }

    [Fact]
    public async Task Invoke_Fails_HandlerNotRun()
    {
        var passing = new FixedValidator();

        var result = await InvokeAsync(passing, new FixedValidator(SpeakerErrors.NameRequired.At("newName")));

        result.IsFailure.Should().BeTrue();
        _handlerCalls.Should().Be(0);
        passing.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Invoke_AllValidatorsPass_CallsNext()
    {
        var first = new FixedValidator();
        var second = new FixedValidator();

        var result = await InvokeAsync(first, second);

        result.IsSuccess.Should().BeTrue();
        _handlerCalls.Should().Be(1);
        (first.Calls, second.Calls).Should().Be((1, 1));
    }

    [Fact]
    public async Task Invoke_ValueResult_FailsWithTheErrors()
    {
        var middleware = new ValidationMiddleware<FindSpeakerQuery, Result<string>>(
            [new FixedQueryValidator(SpeakerErrors.NotFound.ToError())]);

        var result = await middleware.InvokeAsync(
            new FindSpeakerQuery(), _ => ValueTask.FromResult(Result<string>.Success("Ada")), TestContext.Current.CancellationToken);

        result.Errors.Should().ContainSingle().Which.Code.Should().Be(SpeakerErrors.NotFound.Code);
    }

    private ValueTask<Result> InvokeAsync(params IValidator<RenameSpeakerCommand>[] validators) =>
        new ValidationMiddleware<RenameSpeakerCommand, Result>(validators).InvokeAsync(
            new RenameSpeakerCommand("Ada Example"),
            _ =>
            {
                _handlerCalls++;
                return ValueTask.FromResult(Result.Success());
            },
            TestContext.Current.CancellationToken);

    /// <summary>A validator that returns the given errors, or a success when there are none, and counts its calls.</summary>
    private sealed class FixedValidator(params Error[] errors) : IValidator<RenameSpeakerCommand>
    {
        public int Calls { get; private set; }

        public ValueTask<Result> ValidateAsync(RenameSpeakerCommand instance, CancellationToken ct)
        {
            Calls++;
            return ValueTask.FromResult(errors.Length == 0 ? Result.Success() : Result.Failure(errors));
        }
    }

    /// <summary>A validator of the query that fails with the given error.</summary>
    private sealed class FixedQueryValidator(Error error) : IValidator<FindSpeakerQuery>
    {
        public ValueTask<Result> ValidateAsync(FindSpeakerQuery instance, CancellationToken ct) => ValueTask.FromResult(Result.Failure(error));
    }
}

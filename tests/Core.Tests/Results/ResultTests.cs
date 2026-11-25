using YAF.Core.Results;

namespace Core.Tests.Results;

public sealed class ResultTests
{
    private static readonly ErrorDescriptor s_full = new("test.session.full", ErrorKind.Conflict, "Session is full.");
    private static readonly ErrorDescriptor s_closed = new("test.session.closed", ErrorKind.Conflict, "Session is closed.");

    #region default-result
    [Fact]
    public void Default_IsFailureWithUninitialised()
    {
        var result = default(Result<int>);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("core.uninitialised");
    }
    #endregion

    [Fact]
    public void DefaultNonGeneric_IsFailureWithUninitialised()
    {
        var result = default(Result);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(CoreErrors.Uninitialised.Code);
    }

    [Fact]
    public void Uninitialised_IsFailureKind()
    {
        var descriptor = CoreErrors.Uninitialised;

        descriptor.Should().Be(new ErrorDescriptor("core.uninitialised", ErrorKind.Failure, "The result was not initialised."));
    }

    [Fact]
    public void Value_OnFailure_Throws()
    {
        Result<int> result = s_full;

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Value_OnDefault_Throws()
    {
        var result = default(Result<string>);

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ImplicitConversion_FromValue_IsSuccess()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Errors_OnSuccess_IsEmpty()
    {
        var result = Result.Success("value");

        result.Errors.Should().BeEmpty();
        result.IsFailure.Should().BeFalse();
    }

    [Fact]
    public void Errors_OnSuccess_ReturnsSameInstance()
    {
        var first = Result.Success(1).Errors;

        var second = Result.Success().Errors;

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void FirstError_OnSuccess_Throws()
    {
        var result = Result.Success();

        var act = () => result.FirstError;

        act.Should().Throw<InvalidOperationException>();
    }

    #region interface-value
    [Fact]
    public void Success_InterfaceValue_UsesResultSuccess()
    {
        IReadOnlyList<string> titles = ["Keynote", "Workshop"];

        var result = Result.Success(titles);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(titles);
    }
    #endregion

    [Fact]
    public void ImplicitConversion_FromError_IsFailure()
    {
        Result<int> result = s_full.ToError();

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be(s_full.Code);
    }

    [Fact]
    public void ImplicitConversion_FromErrorDescriptor_IsFailure()
    {
        Result result = s_full;

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be(s_full.Code);
    }

    [Fact]
    public void ImplicitConversion_FromErrorArray_IsFailure()
    {
        Error[] errors = [s_full.ToError(), s_closed.ToError()];

        Result<int> result = errors;

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Code).Should().Equal(s_full.Code, s_closed.Code);
    }

    [Fact]
    public void ImplicitConversion_FromErrorArray_CopiesErrors()
    {
        Error[] errors = [s_full.ToError()];
        Result result = errors;

        errors[0] = s_closed.ToError();

        result.FirstError.Code.Should().Be(s_full.Code);
    }

    [Fact]
    public void ImplicitConversion_FromErrorList_IsFailure()
    {
        List<Error> errors = [s_closed.ToError(), s_full.ToError()];

        Result result = errors;

        result.Errors.Select(e => e.Code).Should().Equal(s_closed.Code, s_full.Code);
    }

    #region propagate-errors
    [Fact]
    public void ImplicitConversion_FromOtherResultErrors_PropagatesFailure()
    {
        Result<int> other = s_full;

        Result<string> result = other.Errors.ToArray();

        result.FirstError.Code.Should().Be(s_full.Code);
    }
    #endregion

    [Fact]
    public void Failure_FromEnumerable_KeepsOrder()
    {
        var result = Result.Failure(new[] { s_closed.ToError(), s_full.ToError() }.AsEnumerable());

        result.Errors.Select(e => e.Code).Should().Equal(s_closed.Code, s_full.Code);
    }

    [Fact]
    public void Failure_GenericFromError_IsFailure()
    {
        var result = Result<int>.Failure(s_full.ToError());

        result.FirstError.Code.Should().Be(s_full.Code);
    }

    [Fact]
    public void Failure_NoErrors_Throws()
    {
        var act = () => Result<int>.Failure([]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Failure_NullError_Throws()
    {
        var act = () => Result.Failure([s_full.ToError(), null!]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsSuccess_ThroughInterface_ReadsResult()
    {
        var result = AsInterface(Result.Success(1));

        result.IsSuccess.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Errors_ThroughInterfaceOnDefault_IsUninitialised()
    {
        var result = AsInterface(default(Result<int>));

        result.Errors.Should().ContainSingle().Which.Code.Should().Be(CoreErrors.Uninitialised.Code);
    }

    private static IResult AsInterface(IResult result) => result;
}

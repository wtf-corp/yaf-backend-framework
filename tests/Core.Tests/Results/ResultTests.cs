using YAF.Core.Results;

namespace Core.Tests.Results;

public sealed class ResultTests
{
    private static readonly ErrorDescriptor Full = new("test.session.full", ErrorKind.Conflict, "Session is full.");
    private static readonly ErrorDescriptor Closed = new("test.session.closed", ErrorKind.Conflict, "Session is closed.");

    [Fact]
    public void Default_IsFailureWithUninitialised()
    {
        var result = default(Result<int>);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("core.uninitialised");
    }

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
        Result<int> result = Full;

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

    [Fact]
    public void Success_InterfaceValue_UsesResultSuccess()
    {
        IReadOnlyList<string> titles = ["Keynote", "Workshop"];

        var result = Result.Success(titles);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(titles);
    }

    [Fact]
    public void ImplicitConversion_FromError_IsFailure()
    {
        Result<int> result = Full.ToError();

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be(Full.Code);
    }

    [Fact]
    public void ImplicitConversion_FromErrorDescriptor_IsFailure()
    {
        Result result = Full;

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be(Full.Code);
    }

    [Fact]
    public void ImplicitConversion_FromErrorArray_IsFailure()
    {
        Error[] errors = [Full.ToError(), Closed.ToError()];

        Result<int> result = errors;

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Code).Should().Equal(Full.Code, Closed.Code);
    }

    [Fact]
    public void ImplicitConversion_FromErrorArray_CopiesErrors()
    {
        Error[] errors = [Full.ToError()];
        Result result = errors;

        errors[0] = Closed.ToError();

        result.FirstError.Code.Should().Be(Full.Code);
    }

    [Fact]
    public void ImplicitConversion_FromErrorList_IsFailure()
    {
        List<Error> errors = [Closed.ToError(), Full.ToError()];

        Result result = errors;

        result.Errors.Select(e => e.Code).Should().Equal(Closed.Code, Full.Code);
    }

    [Fact]
    public void ImplicitConversion_FromOtherResultErrors_PropagatesFailure()
    {
        Result<int> other = Full;

        Result<string> result = other.Errors.ToArray();

        result.FirstError.Code.Should().Be(Full.Code);
    }

    [Fact]
    public void Failure_FromEnumerable_KeepsOrder()
    {
        var result = Result.Failure(new[] { Closed.ToError(), Full.ToError() }.AsEnumerable());

        result.Errors.Select(e => e.Code).Should().Equal(Closed.Code, Full.Code);
    }

    [Fact]
    public void Failure_GenericFromError_IsFailure()
    {
        var result = Result<int>.Failure(Full.ToError());

        result.FirstError.Code.Should().Be(Full.Code);
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
        var act = () => Result.Failure([Full.ToError(), null!]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsSuccess_ThroughInterface_ReadsResult()
    {
        IResult result = Result.Success(1);

        result.IsSuccess.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Errors_ThroughInterfaceOnDefault_IsUninitialised()
    {
        IResult result = default(Result<int>);

        result.Errors.Should().ContainSingle().Which.Code.Should().Be(CoreErrors.Uninitialised.Code);
    }
}

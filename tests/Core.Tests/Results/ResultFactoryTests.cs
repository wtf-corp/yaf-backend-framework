using YAF.Core.Results;

namespace Core.Tests.Results;

public sealed class ResultFactoryTests
{
    private static readonly ErrorDescriptor s_full = new("test.session.full", ErrorKind.Conflict, "Session is full.");
    private static readonly ErrorDescriptor s_closed = new("test.session.closed", ErrorKind.Conflict, "Session is closed.");

    [Fact]
    public void Failure_GenericResult_CarriesTheErrors()
    {
        Error[] errors = [s_full.ToError(), s_closed.ToError()];

        var result = ResultFactory.Failure<Result<int>>(errors);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Code).Should().Equal("test.session.full", "test.session.closed");
    }

    [Fact]
    public void Failure_NonGenericResult_CarriesTheErrors()
    {
        Error[] errors = [s_closed.ToError()];

        var result = ResultFactory.Failure<Result>(errors);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be("test.session.closed");
    }

    [Fact]
    public void Failure_InterfaceValueType_Works()
    {
        Error[] errors = [s_full.ToError()];

        var result = ResultFactory.Failure<Result<IDisposable>>(errors);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be("test.session.full");
    }

    [Fact]
    public void Failure_EmptyList_Throws()
    {
        var act = () => ResultFactory.Failure<Result<int>>([]);

        act.Should().Throw<ArgumentException>().WithParameterName("errors");
    }

    [Fact]
    public void Failure_OtherResultType_ThrowsNamingIt()
    {
        Error[] errors = [s_full.ToError()];

        var act = () => ResultFactory.Failure<OtherResult>(errors);

        act.Should().Throw<InvalidOperationException>().WithMessage("*OtherResult*");
    }

    private readonly struct OtherResult : IResult
    {
        public bool IsSuccess => false;

        public IReadOnlyList<Error> Errors => [];
    }
}

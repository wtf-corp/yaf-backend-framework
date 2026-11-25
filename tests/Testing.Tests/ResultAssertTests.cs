using YAF.Core.Results;
using YAF.Testing;

namespace Testing.Tests;

public sealed class ResultAssertTests
{
    [Fact]
    public void ShouldSucceed_Success_Passes()
    {
        var result = Result.Success();

        var act = () => result.ShouldSucceed();

        act.Should().NotThrow();
    }

    [Fact]
    public void ShouldSucceed_Failure_ThrowsListingTheCodes()
    {
        Result result = new[] { Errors.SessionFull.ToError(), Errors.TitleRequired.ToError() };

        var act = () => result.ShouldSucceed();

        act.Should().Throw<YafAssertionException>()
            .Which.Message.Should().Contain($"'{Errors.SessionFull.Code}'").And.Contain($"'{Errors.TitleRequired.Code}'");
    }

    [Fact]
    public void ShouldSucceed_GenericSuccess_ReturnsTheValue()
    {
        var result = Result.Success(42);

        var value = result.ShouldSucceed();

        value.Should().Be(42);
    }

    [Fact]
    public void ShouldSucceed_GenericFailure_ThrowsListingTheCodes()
    {
        Result<int> result = Errors.SessionFull;

        var act = () => result.ShouldSucceed();

        act.Should().Throw<YafAssertionException>().Which.Message.Should().Contain($"'{Errors.SessionFull.Code}'");
    }

    #region result-assert
    [Fact]
    public void ShouldFailWith_SameCode_Passes()
    {
        Result result = Errors.SessionFull.With(("Capacity", 40));

        var act = () => result.ShouldFailWith(Errors.SessionFull);

        act.Should().NotThrow();
    }
    #endregion

    [Fact]
    public void ShouldFailWith_CodeAmongSeveral_Passes()
    {
        Result result = new[] { Errors.TitleRequired.ToError(), Errors.SessionFull.ToError() };

        var act = () => result.ShouldFailWith(Errors.SessionFull);

        act.Should().NotThrow();
    }

    [Fact]
    public void ShouldFailWith_OtherCode_ThrowsListingTheCodes()
    {
        Result result = new[] { Errors.TitleRequired.ToError(), Errors.EmailTaken.ToError() };

        var act = () => result.ShouldFailWith(Errors.SessionFull);

        act.Should().Throw<YafAssertionException>()
            .Which.Message.Should().Contain($"'{Errors.SessionFull.Code}'")
            .And.Contain($"'{Errors.TitleRequired.Code}'")
            .And.Contain($"'{Errors.EmailTaken.Code}'");
    }

    [Fact]
    public void ShouldFailWith_Success_Throws()
    {
        var result = Result.Success();

        var act = () => result.ShouldFailWith(Errors.SessionFull);

        act.Should().Throw<YafAssertionException>().Which.Message.Should().Contain($"'{Errors.SessionFull.Code}'");
    }

    [Fact]
    public void ShouldFailWith_GenericSameCode_Passes()
    {
        Result<int> result = Errors.SessionFull;

        var act = () => result.ShouldFailWith(Errors.SessionFull);

        act.Should().NotThrow();
    }

    [Fact]
    public void ShouldFailWith_GenericSuccess_Throws()
    {
        var result = Result.Success(42);

        var act = () => result.ShouldFailWith(Errors.SessionFull);

        act.Should().Throw<YafAssertionException>().Which.Message.Should().Contain($"'{Errors.SessionFull.Code}'");
    }

    [Fact]
    public void ShouldFailWith_NullDescriptor_ThrowsArgumentNullException()
    {
        Result result = Errors.SessionFull;

        var act = () => result.ShouldFailWith(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("expected");
    }

    [Fact]
    public void Message_NeverContainsTheErrorMessage()
    {
        Result result = Errors.EmailTaken.With(("Email", "attendee-0001@example.test"));

        var act = () => result.ShouldFailWith(Errors.SessionFull);

        act.Should().Throw<YafAssertionException>()
            .Which.Message.Should().Contain($"'{Errors.EmailTaken.Code}'")
            .And.NotContain("attendee-0001@example.test")
            .And.NotContain("already registered");
    }

    private abstract class Errors : IErrorCatalog
    {
        public static readonly ErrorDescriptor SessionFull = Error.Conflict("Session has no free seats (capacity {Capacity}).");

        public static readonly ErrorDescriptor TitleRequired = Error.Validation("A session needs a title.");

        public static readonly ErrorDescriptor EmailTaken = Error.Conflict("The address {Email} is already registered.");
    }
}

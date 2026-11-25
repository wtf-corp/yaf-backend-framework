using YAF.Core.Results;

namespace Core.Tests.Results;

public sealed class ResultUnionTests
{
    private static readonly ErrorDescriptor s_full = new("test.session.full", ErrorKind.Conflict, "Session is full.");

    [Fact]
    public void Switch_ValueAndErrorArms_IsExhaustive()
    {
        Result<int> result = 42;

        var outcome = Describe(result);

        outcome.Should().Be("value 42");
    }

    [Fact]
    public void Switch_Failure_TakesErrorArm()
    {
        Result<int> result = s_full;

        var outcome = Describe(result);

        outcome.Should().Be("error test.session.full");
    }

    [Fact]
    public void Switch_Default_TakesErrorArm()
    {
        var result = default(Result<int>);

        var outcome = Describe(result);

        outcome.Should().Be("error core.uninitialised");
    }

    [Fact]
    public void TryGetValue_Success_ReturnsValue()
    {
        Result<string>.IUnionMembers members = Result.Success("seat-1");

        var found = members.TryGetValue(out string value);

        found.Should().BeTrue();
        value.Should().Be("seat-1");
    }

    [Fact]
    public void TryGetValue_Failure_ReturnsNoValue()
    {
        Result<string>.IUnionMembers members = (Result<string>)s_full;

        var found = members.TryGetValue(out string _);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryGetErrors_Default_ReturnsUninitialised()
    {
        Result<string>.IUnionMembers members = default(Result<string>);

        var found = members.TryGetValue(out IReadOnlyList<Error> errors);

        found.Should().BeTrue();
        errors.Should().ContainSingle().Which.Code.Should().Be(CoreErrors.Uninitialised.Code);
    }

    [Fact]
    public void TryGetErrors_Success_ReturnsNoErrors()
    {
        Result<string>.IUnionMembers members = Result.Success("seat-1");

        var found = members.TryGetValue(out IReadOnlyList<Error> _);

        found.Should().BeFalse();
    }

    [Fact]
    public void HasValue_Default_IsTrue()
    {
        Result<int>.IUnionMembers members = default(Result<int>);

        var hasValue = members.HasValue;

        hasValue.Should().BeTrue();
    }

    [Fact]
    public void UnionValue_Success_IsTheValue()
    {
        Result<int>.IUnionMembers members = Result.Success(42);

        var value = members.Value;

        value.Should().Be(42);
    }

    [Fact]
    public void UnionValue_Default_IsTheErrors()
    {
        Result<int>.IUnionMembers members = default(Result<int>);

        var value = members.Value;

        value.Should().BeAssignableTo<IReadOnlyList<Error>>().Which.Should().ContainSingle().Which.Code.Should().Be(CoreErrors.Uninitialised.Code);
    }

    [Fact]
    public void UnionConversion_FromInterfaceValue_IsSuccess()
    {
        IEnumerable<int> seats = [1, 2];

        Result<IEnumerable<int>> result = seats;

        result.Value.Should().BeSameAs(seats);
    }

    [Fact]
    public void UnionConversion_FromErrorList_IsFailureWithThoseErrors()
    {
        IReadOnlyList<Error> errors = [s_full.ToError()];

        Result<int> result = errors;

        result.Errors.Should().Equal(errors);
    }

    [Fact]
    public void UnionConversion_FromEmptyErrorList_Throws()
    {
        IReadOnlyList<Error> errors = [];

        var act = () => (Result<int>)errors;

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_Errors_IsFailureWithThoseErrors()
    {
        var result = CreateFailure<Result<string>, string>([s_full.ToError()]);

        result.FirstError.Code.Should().Be(s_full.Code);
    }

    [Fact]
    public void Switch_Success_AllocatesNothing()
    {
        Classify(Result.Success(1));

        var before = GC.GetAllocatedBytesForCurrentThread();
        var outcome = Classify(Result.Success(2));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        outcome.Should().Be(2);
        allocated.Should().Be(0);
    }

    #region exhaustive-switch
    private static string Describe(Result<int> result) => result switch
    {
        int value => $"value {value}",
        IReadOnlyList<Error> errors => $"error {errors[0].Code}",
    };
    #endregion

    private static int Classify(Result<int> result) => result switch
    {
        int value => value,
        IReadOnlyList<Error> => -1,
    };

    private static Result<T> CreateFailure<TUnion, T>(IReadOnlyList<Error> errors)
        where TUnion : Result<T>.IUnionMembers => TUnion.Create(errors);
}

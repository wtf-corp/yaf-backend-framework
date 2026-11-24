using YAF.Core.Results;

namespace Core.Tests.Results;

public sealed class ErrorFactoryTests
{
    // Core.Tests runs no error code generator, so every factory call reaches the method body.
    public static TheoryData<Func<string, ErrorDescriptor>> Factories() => new()
    {
        Error.Validation,
        Error.Unauthenticated,
        Error.Forbidden,
        Error.NotFound,
        Error.Conflict,
        Error.PreconditionFailed,
        Error.RateLimited,
        Error.Unavailable,
        Error.Failure,
    };

    [Fact]
    public void NotFound_WithoutGenerator_Throws()
    {
        var act = () => Error.NotFound("Session was not found.");

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("YAF.Core").And.Contain("YafServiceCode");
    }

    [Theory]
    [MemberData(nameof(Factories))]
    public void Factory_WithoutGenerator_Throws(Func<string, ErrorDescriptor> factory)
    {
        var act = () => factory("A message.");

        act.Should().Throw<InvalidOperationException>().WithMessage("*YafServiceCode*");
    }

    [Fact]
    public void FactoryInStaticFieldInitialiser_WithoutGenerator_ThrowsAtTypeInitialisation()
    {
        var act = () => SampleCatalogue.Errors.Missing.Code;

        act.Should().Throw<TypeInitializationException>()
            .WithInnerException<InvalidOperationException>();
    }

    [Fact]
    public void ErrorCodeAttribute_Code_IsKept()
    {
        var attribute = new ErrorCodeAttribute("catalog.session.title_required");

        attribute.Code.Should().Be("catalog.session.title_required");
    }

    private static class SampleCatalogue
    {
        internal abstract class Errors : IErrorCatalog
        {
            public static readonly ErrorDescriptor Missing = Error.NotFound("Sample was not found.");
        }
    }
}

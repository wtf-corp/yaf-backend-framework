using YAF.Core.Results;

namespace Core.Tests.Results;

public sealed class ErrorDescriptorTests
{
    private static readonly ErrorDescriptor s_full =
        new("test.session.full", ErrorKind.Conflict, "Session has no free seats (capacity {Capacity}).");

    #region fill-arguments
    [Fact]
    public void With_NamedArgument_FillsPlaceholder()
    {
        var error = s_full.With(("Capacity", 40));

        error.Message.Should().Be("Session has no free seats (capacity 40).");
    }
    #endregion

    [Fact]
    public void With_Arguments_KeepsArgumentsAsGiven()
    {
        var error = s_full.With(("Capacity", 40), ("Unused", null));

        error.Arguments.Should().HaveCount(2);
        error.Arguments.Should().Contain("Capacity", 40);
        error.Arguments.Should().Contain("Unused", null);
    }

    [Fact]
    public void With_MissingArgument_LeavesPlaceholder()
    {
        var error = s_full.With(("Other", 1));

        error.Message.Should().Be("Session has no free seats (capacity {Capacity}).");
    }

    [Fact]
    public void With_ArgumentNameInOtherCase_LeavesPlaceholder()
    {
        var error = s_full.With(("capacity", 40));

        error.Message.Should().Be("Session has no free seats (capacity {Capacity}).");
    }

    [Fact]
    public void With_PlaceholderTwice_FillsBoth()
    {
        var descriptor = new ErrorDescriptor("test.move", ErrorKind.Validation, "{From} to {To}, not {From}.");

        var error = descriptor.With(("From", "Draft"), ("To", "Published"));

        error.Message.Should().Be("Draft to Published, not Draft.");
    }

    [Fact]
    public void With_FormattableValue_UsesInvariantCulture()
    {
        var descriptor = new ErrorDescriptor("test.price", ErrorKind.Validation, "Price {Price} is too high.");

        var error = descriptor.With(("Price", 1234.5m));

        error.Message.Should().Be("Price 1234.5 is too high.");
    }

    [Fact]
    public void ToError_Always_UsesTemplateWithoutTargetOrArguments()
    {
        var error = s_full.ToError();

        error.Code.Should().Be("test.session.full");
        error.Kind.Should().Be(ErrorKind.Conflict);
        error.Message.Should().Be("Session has no free seats (capacity {Capacity}).");
        error.Target.Should().BeNull();
        error.Arguments.Should().BeEmpty();
    }

    #region target
    [Fact]
    public void At_Target_SetsTarget()
    {
        var descriptor = new ErrorDescriptor("test.email.invalid", ErrorKind.Validation, "Email is invalid.");

        var error = descriptor.At("speakers[1].email");

        error.Target.Should().Be("speakers[1].email");
        error.Code.Should().Be("test.email.invalid");
        error.Message.Should().Be("Email is invalid.");
    }
    #endregion
}

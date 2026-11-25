using YAF.Core;

namespace Core.Tests;

public sealed class GuardTests
{
    [Fact]
    public void NotNull_Null_ThrowsWithArgumentName()
    {
        object? owner = null;

        var act = () => Guard.NotNull(owner);

        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("owner");
    }

    [Fact]
    public void NotNull_Value_DoesNotThrow()
    {
        var owner = new object();

        var act = () => Guard.NotNull(owner);

        act.Should().NotThrow();
    }

    [Fact]
    public void NotNullOrWhiteSpace_Null_ThrowsArgumentNull()
    {
        string? name = null;

        var act = () => Guard.NotNullOrWhiteSpace(name);

        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("name");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void NotNullOrWhiteSpace_Blank_Throws(string name)
    {
        var act = () => Guard.NotNullOrWhiteSpace(name);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("name");
    }

    [Fact]
    public void NotNullOrWhiteSpace_Text_DoesNotThrow()
    {
        var name = "Ada";

        var act = () => Guard.NotNullOrWhiteSpace(name);

        act.Should().NotThrow();
    }

    [Fact]
    public void NotDefault_DefaultStruct_ThrowsWithArgumentName()
    {
        var sessionId = default(Guid);

        var act = () => Guard.NotDefault(sessionId);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("sessionId");
    }

    [Fact]
    public void NotDefault_Value_DoesNotThrow()
    {
        var sessionId = Guid.CreateVersion7();

        var act = () => Guard.NotDefault(sessionId);

        act.Should().NotThrow();
    }
}

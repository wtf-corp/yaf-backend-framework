using YAF.Testing;

namespace Testing.Tests;

public sealed class SyntheticDataTests
{
    [Fact]
    public void Email_IsPaddedAndOnExampleTest()
    {
        var email = SyntheticData.Email(1);

        email.Should().Be("attendee-0001@example.test");
    }

    [Fact]
    public void Email_WithRole_StartsWithTheRole()
    {
        var email = SyntheticData.Email(42, "speaker");

        email.Should().Be("speaker-0042@example.test");
    }

    [Fact]
    public void Email_NegativeIndex_Throws()
    {
        var act = () => SyntheticData.Email(-1);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("index");
    }

    [Fact]
    public void Name_IsPadded()
    {
        var name = SyntheticData.Name(1);

        name.Should().Be("Attendee 0001");
    }

    [Fact]
    public void Name_WithRole_StartsWithTheRole()
    {
        var name = SyntheticData.Name(42, "Speaker");

        name.Should().Be("Speaker 0042");
    }

    [Fact]
    public void Name_NegativeIndex_Throws()
    {
        var act = () => SyntheticData.Name(-1);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("index");
    }
}

using YAF.Domain;

namespace Domain.Tests.Aggregates;

public sealed class AggregateVersionTests
{
    [Fact]
    public void From_Guid_KeepsTheValue()
    {
        var value = Guid.CreateVersion7();

        var version = AggregateVersion.From(value);

        version.Value.Should().Be(value);
    }

    [Fact]
    public void Default_NeverSaved_IsTheEmptyGuid()
    {
        var version = default(AggregateVersion);

        version.Value.Should().Be(Guid.Empty);
    }
}

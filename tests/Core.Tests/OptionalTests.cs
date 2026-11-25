using YAF.Core;

namespace Core.Tests;

public sealed class OptionalTests
{
    [Fact]
    public void Default_IsNotSupplied()
    {
        Optional<string?> title = default;

        title.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Default_Value_Throws()
    {
        Optional<int> capacity = default;

        var act = () => capacity.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FromNull_IsSupplied()
    {
        Optional<string?> title = null;

        title.HasValue.Should().BeTrue();
        title.Value.Should().BeNull();
    }

    [Fact]
    public void FromValue_IsSuppliedWithValue()
    {
        Optional<int> capacity = 120;

        capacity.HasValue.Should().BeTrue();
        capacity.Value.Should().Be(120);
    }

    [Fact]
    public void FromDefaultOfT_IsSupplied()
    {
        Optional<int> capacity = 0;

        capacity.HasValue.Should().BeTrue();
        capacity.Value.Should().Be(0);
    }

    [Fact]
    public void Equals_TwoDefaults_AreEqual()
    {
        Optional<string?> left = default;
        Optional<string?> right = default;

        (left == right).Should().BeTrue();
    }

    [Fact]
    public void Equals_SuppliedNullAndNotSupplied_AreNotEqual()
    {
        Optional<string?> supplied = null;
        Optional<string?> notSupplied = default;

        (supplied != notSupplied).Should().BeTrue();
    }

    [Fact]
    public void Equals_SuppliedDefaultOfTAndNotSupplied_AreNotEqual()
    {
        Optional<int> supplied = 0;
        Optional<int> notSupplied = default;

        supplied.Equals(notSupplied).Should().BeFalse();
    }

    [Fact]
    public void Equals_SameSuppliedValue_AreEqualWithSameHashCode()
    {
        Optional<string?> left = "Keynote";
        Optional<string?> right = "Keynote";

        left.Equals((object)right).Should().BeTrue();
        left.GetHashCode().Should().Be(right.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentSuppliedValues_AreNotEqual()
    {
        Optional<string?> left = "Keynote";
        Optional<string?> right = "Workshop";

        (left == right).Should().BeFalse();
    }

    [Fact]
    public void Equals_OtherType_IsFalse()
    {
        Optional<int> capacity = 120;

        capacity.Equals((object)120).Should().BeFalse();
    }
}

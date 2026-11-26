using YAF.Core.Results;
using YAF.Domain.ValueObjects;

namespace Domain.Tests.ValueObjects;

public sealed class PeriodTests
{
    private static readonly DateTimeOffset s_nine = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Contains_End_IsExcluded()
    {
        var period = Period.Create(s_nine, s_nine.AddHours(1)).Value;

        period.Contains(s_nine.AddHours(1)).Should().BeFalse();
    }

    [Fact]
    public void Contains_SameInstantInAnotherOffset_IsIncluded()
    {
        var period = Period.Create(s_nine, s_nine.AddHours(1)).Value;

        period.Contains(new DateTimeOffset(2026, 11, 26, 10, 30, 0, TimeSpan.FromHours(1))).Should().BeTrue();
    }

    [Fact]
    public void Create_EndBeforeStart_Fails()
    {
        var result = Period.Create(s_nine, s_nine.AddTicks(-1));

        result.Errors.Select(e => e.Code).Should().Equal(ValueObjectErrors.PeriodEndBeforeStart.Code);
        result.FirstError.Kind.Should().Be(ErrorKind.Validation);
        ValueObjectErrors.PeriodEndBeforeStart.Code.Should().Be("core.period.end_before_start");
    }

    [Fact]
    public void Create_EndEqualsStart_IsEmptyAndContainsNothing()
    {
        var result = Period.Create(s_nine, s_nine);

        result.IsSuccess.Should().BeTrue();
        result.Value.Contains(s_nine).Should().BeFalse();
    }

    [Fact]
    public void Overlaps_Touching_IsFalse()
    {
        var morning = Period.Create(s_nine, s_nine.AddHours(1)).Value;
        var next = Period.Create(s_nine.AddHours(1), s_nine.AddHours(2)).Value;

        morning.Overlaps(next).Should().BeFalse();
        next.Overlaps(morning).Should().BeFalse();
    }

    [Fact]
    public void Overlaps_EmptyInside_IsFalse()
    {
        var period = Period.Create(s_nine, s_nine.AddHours(2)).Value;
        var empty = Period.Create(s_nine.AddHours(1), s_nine.AddHours(1)).Value;

        period.Overlaps(empty).Should().BeFalse();
        empty.Overlaps(period).Should().BeFalse();
    }

    [Fact]
    public void Intersect_Disjoint_IsNull()
    {
        var morning = Period.Create(s_nine, s_nine.AddHours(1)).Value;
        var afternoon = Period.Create(s_nine.AddHours(5), s_nine.AddHours(6)).Value;

        morning.Intersect(afternoon).Should().BeNull();
    }

    [Fact]
    public void Intersect_Overlapping_ReturnsTheSharedInstants()
    {
        var first = Period.Create(s_nine, s_nine.AddHours(2)).Value;
        var second = Period.Create(s_nine.AddHours(1), s_nine.AddHours(3)).Value;

        first.Intersect(second).Should().Be(Period.Create(s_nine.AddHours(1), s_nine.AddHours(2)).Value);
    }
}

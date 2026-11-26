using YAF.Core.Results;
using YAF.Domain.ValueObjects;

namespace Domain.Tests.ValueObjects;

public sealed class DateRangeTests
{
    private static readonly DateOnly s_first = new(2026, 11, 1);

    [Fact]
    public void Contains_End_IsIncluded()
    {
        var range = DateRange.Create(s_first, s_first.AddDays(2)).Value;

        range.Contains(s_first.AddDays(2)).Should().BeTrue();
    }

    [Fact]
    public void Contains_DayAfterEnd_IsExcluded()
    {
        var range = DateRange.Create(s_first, s_first.AddDays(2)).Value;

        range.Contains(s_first.AddDays(3)).Should().BeFalse();
    }

    [Fact]
    public void Create_EndBeforeStart_Fails()
    {
        var result = DateRange.Create(s_first, s_first.AddDays(-1));

        result.Errors.Select(e => e.Code).Should().Equal(ValueObjectErrors.DateRangeEndBeforeStart.Code);
        result.FirstError.Kind.Should().Be(ErrorKind.Validation);
        ValueObjectErrors.DateRangeEndBeforeStart.Code.Should().Be("core.date_range.end_before_start");
    }

    [Fact]
    public void Create_SingleDay_ContainsThatDay()
    {
        var result = DateRange.Create(s_first, s_first);

        result.IsSuccess.Should().BeTrue();
        result.Value.Contains(s_first).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_SharedDay_IsTrue()
    {
        var first = DateRange.Create(s_first, s_first.AddDays(2)).Value;
        var second = DateRange.Create(s_first.AddDays(2), s_first.AddDays(4)).Value;

        first.Overlaps(second).Should().BeTrue();
        second.Overlaps(first).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_Adjacent_IsFalse()
    {
        var first = DateRange.Create(s_first, s_first.AddDays(2)).Value;
        var second = DateRange.Create(s_first.AddDays(3), s_first.AddDays(4)).Value;

        first.Overlaps(second).Should().BeFalse();
    }

    [Fact]
    public void Intersect_Overlapping_ReturnsTheSharedDays()
    {
        var first = DateRange.Create(s_first, s_first.AddDays(5)).Value;
        var second = DateRange.Create(s_first.AddDays(3), s_first.AddDays(9)).Value;

        first.Intersect(second).Should().Be(DateRange.Create(s_first.AddDays(3), s_first.AddDays(5)).Value);
    }
}

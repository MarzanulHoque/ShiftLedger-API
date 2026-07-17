using FluentAssertions;
using ShiftLedger.Application.Common.Time;
using Xunit;

namespace ShiftLedger.Application.UnitTests.Common.Time;

public class DateBucketsTests
{
    // Rule T5: week boundaries come from date-only fields + WeekStartDay (no time zone).
    [Theory]
    [InlineData("2026-07-15", "2026-07-13")] // Wed -> Mon (week starts Monday)
    [InlineData("2026-07-13", "2026-07-13")] // Mon -> same day
    [InlineData("2026-07-19", "2026-07-13")] // Sun -> previous Mon
    public void WeekStart_MondayStart_ReturnsContainingMonday(string date, string expected)
    {
        DateBuckets.WeekStart(DateOnly.Parse(date), DayOfWeek.Monday)
            .Should().Be(DateOnly.Parse(expected));
    }

    [Fact]
    public void MonthStart_ReturnsFirstOfMonth()
    {
        DateBuckets.MonthStart(new DateOnly(2026, 7, 15)).Should().Be(new DateOnly(2026, 7, 1));
    }
}

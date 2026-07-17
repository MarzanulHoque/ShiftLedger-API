namespace ShiftLedger.Application.Common.Time;

// Timezone-independent date grouping (Rules T5/T9): buckets computed from date-only fields + WeekStartDay,
// never from time-zone conversion. The central UTC clock is the injected TimeProvider.
public static class DateBuckets
{
    // First day of the week containing <date>, given the org's week-start day.
    public static DateOnly WeekStart(DateOnly date, DayOfWeek weekStartsOn)
    {
        var diff = ((int)date.DayOfWeek - (int)weekStartsOn + 7) % 7;
        return date.AddDays(-diff);
    }

    // First day of the month containing <date>.
    public static DateOnly MonthStart(DateOnly date) => new(date.Year, date.Month, 1);
}

using ShiftLedger.Domain.Common;

namespace ShiftLedger.Domain.Entities;

// Single-row org configuration: the one source for week-start, currency, and OT multiplier.
// No time zone is stored (Rule T4) — instants are UTC, displayed in the viewer's browser zone.
public class OrgSettings : BaseEntity
{
    public DayOfWeek WeekStartDay { get; set; } = DayOfWeek.Monday;
    public string CurrencyCode { get; set; } = "USD";   // ISO 4217
    public decimal OvertimeMultiplier { get; set; } = 1.5m;
}

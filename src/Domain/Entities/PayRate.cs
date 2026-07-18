using ShiftLedger.Domain.Common;

namespace ShiftLedger.Domain.Entities;

// One entry in an employee's effective-dated pay-rate history (Rule P3). History is
// append-only: a rate is never edited; a new rate closes the prior one by setting its
// EffectiveTo. EffectiveTo == null marks the currently-active rate. Dates are calendar
// dates (no time/offset) so they never shift across time zones (Rule T9).
public class PayRate : BaseEntity
{
    public Guid EmployeeProfileId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

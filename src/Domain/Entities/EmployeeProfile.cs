using ShiftLedger.Domain.Common;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Domain.Entities;

// Pay profile for a user — 1:1 with User (unique UserId). Every Employee has one;
// an Admin only if they are also paid. Holds how (RateType) and how often (PayCycle)
// they are paid; the actual amounts live in the effective-dated PayRate history.
public class EmployeeProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public RateType RateType { get; set; }
    public PayCycle PayCycle { get; set; }
}

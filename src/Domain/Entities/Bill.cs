using ShiftLedger.Domain.Common;

namespace ShiftLedger.Domain.Entities;

// The single bill for a service job (Rule B1: one per job, unique ServiceJobId). The total is
// NEVER stored — it is computed from the line items so it can't drift (Rules B2/C2). Editable
// while unpaid; once IsPaid the bill and its lines are locked (Rule B3).
public class Bill : BaseEntity, ISoftDeletable
{
    public Guid ServiceJobId { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}

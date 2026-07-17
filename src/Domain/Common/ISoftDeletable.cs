namespace ShiftLedger.Domain.Common;

// Soft-deleted (hidden, not removed) entities with referential history — User/Project/WorkTask.
// Global query filter on IsDeleted applied in AppDbContext (P1-6). Immutable financial
// records (approved Payslip, paid ManualPayment) are never deleted and omit this.
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
}

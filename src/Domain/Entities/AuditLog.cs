namespace ShiftLedger.Domain.Entities;

// Append-only audit record (Rules A1/A2): one row per entity state change, never updated or deleted.
// Not a BaseEntity — it is immutable, so it has no concurrency token.
public class AuditLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string EntityName { get; set; } = default!;
    public string EntityId { get; set; } = default!;
    public string Action { get; set; } = default!;   // Created | Modified | Deleted
    public Guid? ChangedById { get; set; }            // set from the current user once auth exists (P2+)
    public DateTime ChangedAtUtc { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
}

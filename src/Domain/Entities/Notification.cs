namespace ShiftLedger.Domain.Entities;

// An in-app notification (persisted so the bell/center is accurate on reload; pushed live via
// SignalR). Not a BaseEntity: it is write-once + mark-read, so it needs no concurrency token,
// and keeping it out of the audit pipeline avoids noise.
public class Notification
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RecipientId { get; set; }
    public string Type { get; set; } = default!;      // JobAssigned | JobStatusChanged | BillPaid
    public string Message { get; set; } = default!;
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

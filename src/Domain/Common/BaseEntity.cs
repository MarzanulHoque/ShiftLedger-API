namespace ShiftLedger.Domain.Common;

public abstract class BaseEntity
{
    // UUIDv7: unique, non-enumerable, time-ordered so it indexes well in MySQL.
    public Guid Id { get; set; } = Guid.CreateVersion7();

    // Optimistic-concurrency token (Rule C1); regenerated on update in SaveChanges (P1-4).
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}

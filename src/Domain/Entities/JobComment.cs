using ShiftLedger.Domain.Common;

namespace ShiftLedger.Domain.Entities;

// A note on a service job. Part of the job's history, so it is not soft-deletable.
public class JobComment : BaseEntity
{
    public Guid ServiceJobId { get; set; }
    public Guid AuthorId { get; set; }
    public string Body { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
}

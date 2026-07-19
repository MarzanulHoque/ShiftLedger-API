using ShiftLedger.Domain.Common;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Domain.Entities;

// A bike brought in for service — the unit of work in v1. Standalone (no project grouping).
// Soft-deletable (Rule J4); assigned to a mechanic, i.e. a User with the Employee role (Rule J2).
public class ServiceJob : BaseEntity, ISoftDeletable
{
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string BikeModel { get; set; } = default!;    // free text in v1 (no Customer/Bike entity)
    public JobStatus Status { get; set; } = JobStatus.Received;
    public JobPriority Priority { get; set; } = JobPriority.Medium;
    public Guid? AssignedMechanicId { get; set; }         // FK User (Employee role)
    public DateOnly ReceivedDate { get; set; }            // calendar date, native DATE (Rule T9)
    public DateOnly? DueDate { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}

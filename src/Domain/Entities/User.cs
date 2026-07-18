using ShiftLedger.Domain.Common;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Domain.Entities;

public class User : BaseEntity, ISoftDeletable
{
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;        // unique, case-insensitive
    public string PasswordHash { get; set; } = default!;
    public Role Role { get; set; }
    public Guid? DepartmentId { get; set; }               // FK relationship wired when Department exists (P3)
    public bool IsActive { get; set; } = true;
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}

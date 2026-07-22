namespace ShiftLedger.Application.Common.Interfaces;

// Rule RB0: the single home for the Super Admin department-boundary bypass. Handlers call this
// instead of re-deriving `currentUser.IsSuperAdmin || currentUser.DepartmentId == x` inline, so
// the bypass rule lives in exactly one place, not one per handler.
public interface IDepartmentScope
{
    bool CanAccess(Guid departmentId);

    // Throws ForbiddenException when the caller cannot act on the given department.
    void EnsureAccess(Guid departmentId);
}

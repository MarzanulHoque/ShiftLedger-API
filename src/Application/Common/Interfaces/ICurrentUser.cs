namespace ShiftLedger.Application.Common.Interfaces;

// The authenticated caller as seen by the Application layer. Implemented in the API from the JWT
// claims (Api/Security/CurrentUser). Used for ownership scoping (Rule R2), department scoping
// (Rule RB3) and audit "who" (Rule A1).
public interface ICurrentUser
{
    Guid? UserId { get; }

    // True for SuperAdmin or DepartmentAdmin (either admin tier). Department-boundary enforcement
    // for a DepartmentAdmin is layered on top of this in the handlers (Rule RB3/RB5, added in P9).
    bool IsAdmin { get; }

    // Rule RB0/RB1: the org-wide super admin, unrestricted across every department.
    bool IsSuperAdmin { get; }

    // Rule RB2: null for SuperAdmin; the caller's own department for DepartmentAdmin/Employee.
    Guid? DepartmentId { get; }

    bool IsAuthenticated { get; }
}

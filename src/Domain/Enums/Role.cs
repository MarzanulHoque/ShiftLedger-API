namespace ShiftLedger.Domain.Enums;

// v2 roles: a 3-tier hierarchy (Rules RB0-RB5). Persisted as string (VARCHAR).
// SuperAdmin is org-wide (DepartmentId null, unrestricted CRUD everywhere - RB0/RB1).
// DepartmentAdmin and Employee are each scoped to exactly one Department (RB2/RB3).
public enum Role
{
    SuperAdmin,
    DepartmentAdmin,
    Employee,
}

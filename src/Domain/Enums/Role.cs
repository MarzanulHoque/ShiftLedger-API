namespace ShiftLedger.Domain.Enums;

// v1 roles (Rule R4: hierarchical roles are post-MVP). Persisted as string (VARCHAR).
public enum Role
{
    Admin,
    Employee,
}

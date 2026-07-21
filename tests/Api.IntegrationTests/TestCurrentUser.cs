using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Api.IntegrationTests;

// A simple ICurrentUser stub for tests: no HttpContext, so identity is supplied directly.
public sealed class TestCurrentUser(Guid? userId, bool isAdmin, bool isSuperAdmin, Guid? departmentId = null) : ICurrentUser
{
    public static TestCurrentUser SuperAdmin(Guid id) => new(id, isAdmin: true, isSuperAdmin: true);
    public static TestCurrentUser DepartmentAdmin(Guid id, Guid departmentId) =>
        new(id, isAdmin: true, isSuperAdmin: false, departmentId);
    public static TestCurrentUser Employee(Guid id, Guid? departmentId = null) =>
        new(id, isAdmin: false, isSuperAdmin: false, departmentId);

    public Guid? UserId { get; } = userId;
    public bool IsAdmin { get; } = isAdmin;
    public bool IsSuperAdmin { get; } = isSuperAdmin;
    public Guid? DepartmentId { get; } = departmentId;
    public bool IsAuthenticated => UserId is not null;
}

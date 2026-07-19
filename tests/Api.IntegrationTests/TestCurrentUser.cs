using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Api.IntegrationTests;

// A simple ICurrentUser stub for tests: no HttpContext, so identity is supplied directly.
public sealed class TestCurrentUser(Guid? userId, bool isAdmin) : ICurrentUser
{
    public static TestCurrentUser Admin(Guid id) => new(id, isAdmin: true);
    public static TestCurrentUser Employee(Guid id) => new(id, isAdmin: false);

    public Guid? UserId { get; } = userId;
    public bool IsAdmin { get; } = isAdmin;
    public bool IsAuthenticated => UserId is not null;
}

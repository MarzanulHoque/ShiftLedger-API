using ShiftLedger.Application.Common.Authorization;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Api.IntegrationTests;

// The real DepartmentScope wired to a stub ICurrentUser — tests build these directly since there's
// no HTTP context to resolve ICurrentUser from.
public static class TestDepartmentScope
{
    public static IDepartmentScope For(ICurrentUser currentUser) => new DepartmentScope(currentUser);
}

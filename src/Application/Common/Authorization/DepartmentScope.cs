using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Application.Common.Authorization;

public class DepartmentScope(ICurrentUser currentUser) : IDepartmentScope
{
    public bool CanAccess(Guid departmentId) =>
        currentUser.IsSuperAdmin || currentUser.DepartmentId == departmentId;

    public void EnsureAccess(Guid departmentId)
    {
        if (!CanAccess(departmentId))
        {
            throw new ForbiddenException();
        }
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Users;

public record UpdateUserCommand(Guid Id, string FullName, Role Role, Guid? DepartmentId, bool IsActive) : IRequest;

public class UpdateUserCommandHandler(IAppDbContext db, ICurrentUser currentUser, IDepartmentScope departmentScope)
    : IRequestHandler<UpdateUserCommand>
{
    public async Task Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        // Rule RB1: the Super Admin's role can never be changed, and no one else may be promoted into it.
        if (user.Role == Role.SuperAdmin || request.Role == Role.SuperAdmin)
        {
            throw new BusinessRuleException("The Super Admin account cannot be edited this way.");
        }

        // Rule RB5: a DepartmentAdmin may only manage Employees within their own department — they
        // can't touch another DepartmentAdmin's record, promote anyone, or move a user to another
        // department. SuperAdmin bypasses (RB0).
        if (!currentUser.IsSuperAdmin)
        {
            if (user.Role != Role.Employee || request.Role != Role.Employee)
            {
                throw new ForbiddenException();
            }
            departmentScope.EnsureAccess(user.DepartmentId ?? throw new ForbiddenException());
            if (request.DepartmentId != currentUser.DepartmentId)
            {
                throw new ForbiddenException();
            }
        }

        user.FullName = request.FullName.Trim();
        user.Role = request.Role;
        user.DepartmentId = request.DepartmentId;
        user.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
    }
}

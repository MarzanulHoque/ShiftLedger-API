using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Users;

public record DeleteUserCommand(Guid Id) : IRequest;

public class DeleteUserCommandHandler(IAppDbContext db, ICurrentUser currentUser, IDepartmentScope departmentScope)
    : IRequestHandler<DeleteUserCommand>
{
    public async Task Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        // Rule RB1: the single Super Admin account can never be deleted.
        if (user.Role == Role.SuperAdmin)
        {
            throw new BusinessRuleException("The Super Admin account cannot be deleted.");
        }

        // Rule RB5: a DepartmentAdmin may only remove Employees within their own department. SuperAdmin bypasses (RB0).
        if (!currentUser.IsSuperAdmin)
        {
            if (user.Role != Role.Employee)
            {
                throw new ForbiddenException();
            }
            departmentScope.EnsureAccess(user.DepartmentId ?? throw new ForbiddenException());
        }

        // Remove maps to a soft delete via the SaveChanges interceptor (User is ISoftDeletable).
        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
    }
}

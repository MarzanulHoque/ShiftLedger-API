using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Users;

public record UpdateUserCommand(Guid Id, string FullName, Role Role, Guid? DepartmentId, bool IsActive) : IRequest;

public class UpdateUserCommandHandler(IAppDbContext db) : IRequestHandler<UpdateUserCommand>
{
    public async Task Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        user.FullName = request.FullName.Trim();
        user.Role = request.Role;
        user.DepartmentId = request.DepartmentId;
        user.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
    }
}

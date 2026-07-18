using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Application.Users;

public record DeleteUserCommand(Guid Id) : IRequest;

public class DeleteUserCommandHandler(IAppDbContext db) : IRequestHandler<DeleteUserCommand>
{
    public async Task Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        // Remove maps to a soft delete via the SaveChanges interceptor (User is ISoftDeletable).
        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
    }
}

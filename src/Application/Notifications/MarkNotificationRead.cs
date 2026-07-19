using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Application.Notifications;

// Mark one of the caller's own notifications read. Scoping by RecipientId means another user's
// notification simply isn't found (404) — nothing leaks about its existence.
public record MarkNotificationReadCommand(Guid Id) : IRequest;

public class MarkNotificationReadCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<MarkNotificationReadCommand>
{
    public async Task Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException();

        var notification = await db.Notifications
                .FirstOrDefaultAsync(n => n.Id == request.Id && n.RecipientId == userId, cancellationToken)
            ?? throw new NotFoundException("Notification not found.");

        notification.IsRead = true;
        await db.SaveChangesAsync(cancellationToken);
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Application.Notifications;

// The caller's own notifications (unread first, then newest). The bell loads these on page load
// and reconciles with live hub pushes — no polling.
public record GetNotificationsQuery(bool UnreadOnly = false) : IRequest<IReadOnlyList<NotificationDto>>;

public class GetNotificationsQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    public async Task<IReadOnlyList<NotificationDto>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException();

        var query = db.Notifications.AsNoTracking().Where(n => n.RecipientId == userId);
        if (request.UnreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        return await query
            .OrderBy(n => n.IsRead)
            .ThenByDescending(n => n.CreatedAtUtc)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Message, n.IsRead, n.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}

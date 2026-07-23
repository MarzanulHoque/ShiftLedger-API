using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Entities;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Notifications;

public record NotificationDto(Guid Id, string Type, string Message, bool IsRead, DateTime CreatedAtUtc);

public class Notifier(IAppDbContext db, IRealtimePusher pusher, TimeProvider timeProvider, ILogger<Notifier> logger)
    : INotifier
{
    public async Task NotifyAsync(Guid recipientId, string type, string message, CancellationToken cancellationToken)
    {
        var notification = new Notification
        {
            RecipientId = recipientId,
            Type = type,
            Message = message,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);

        // Real-time delivery is best-effort: the persisted row is the source of truth (the bell
        // reconciles from GET /notifications on load), so a push failure must not fail the command.
        try
        {
            await pusher.PushAsync(
                recipientId,
                new NotificationDto(notification.Id, notification.Type, notification.Message,
                    notification.IsRead, notification.CreatedAtUtc),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Real-time push failed for notification {NotificationId}", notification.Id);
        }
    }

    public async Task NotifyDepartmentAsync(Guid departmentId, string type, string message, CancellationToken cancellationToken)
    {
        var recipientIds = await db.Users.AsNoTracking()
            .Where(u => u.Role == Role.SuperAdmin || (u.Role == Role.DepartmentAdmin && u.DepartmentId == departmentId))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rows = recipientIds.Select(id => new Notification
        {
            RecipientId = id,
            Type = type,
            Message = message,
            CreatedAtUtc = now,
        }).ToList();
        db.Notifications.AddRange(rows);
        await db.SaveChangesAsync(cancellationToken);

        // The group push is a single "something changed" signal — the client just invalidates its
        // own GET /notifications on receipt (see useNotificationsSocket.ts), so this DTO's Id
        // doesn't need to match any one recipient's specific row.
        try
        {
            await pusher.PushToDepartmentAsync(
                departmentId, new NotificationDto(Guid.NewGuid(), type, message, false, now), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Real-time department push failed for department {DepartmentId}", departmentId);
        }
    }
}

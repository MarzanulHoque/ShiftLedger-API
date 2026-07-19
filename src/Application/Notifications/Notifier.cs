using Microsoft.Extensions.Logging;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Entities;

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
}

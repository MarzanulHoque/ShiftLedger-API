using ShiftLedger.Application.Notifications;

namespace ShiftLedger.Application.Common.Interfaces;

// Raises an in-app notification: persists the Notification row (source of truth for the bell)
// and pushes it live to the recipient's connected clients. Handlers call this AFTER their own
// SaveChanges, so a notification is never pushed for a change that failed to commit.
public interface INotifier
{
    Task NotifyAsync(Guid recipientId, string type, string message, CancellationToken cancellationToken);
}

// The real-time leg only (SignalR in the API layer). Split from INotifier so the Application
// layer never references SignalR and tests can plug a no-op.
public interface IRealtimePusher
{
    Task PushAsync(Guid recipientId, NotificationDto notification, CancellationToken cancellationToken);
}

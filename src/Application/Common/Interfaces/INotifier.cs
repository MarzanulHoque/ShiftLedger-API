using ShiftLedger.Application.Notifications;

namespace ShiftLedger.Application.Common.Interfaces;

// Raises an in-app notification: persists the Notification row (source of truth for the bell)
// and pushes it live to the recipient's connected clients. Handlers call this AFTER their own
// SaveChanges, so a notification is never pushed for a change that failed to commit.
public interface INotifier
{
    Task NotifyAsync(Guid recipientId, string type, string message, CancellationToken cancellationToken);

    // Rules N1/N2/N3: a department-wide event (bill paid, job created/completed/delivered,
    // overdue/unpaid alert) — persists one row per relevant recipient (the SuperAdmin plus that
    // department's DepartmentAdmin(s)) and pushes live to the department's group and the org-wide
    // group, so a SuperAdmin sees every department while a DepartmentAdmin sees only their own.
    Task NotifyDepartmentAsync(Guid departmentId, string type, string message, CancellationToken cancellationToken);
}

// The real-time leg only (SignalR in the API layer). Split from INotifier so the Application
// layer never references SignalR and tests can plug a no-op.
public interface IRealtimePusher
{
    Task PushAsync(Guid recipientId, NotificationDto notification, CancellationToken cancellationToken);
    Task PushToDepartmentAsync(Guid departmentId, NotificationDto notification, CancellationToken cancellationToken);
}

using Microsoft.AspNetCore.SignalR;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Application.Notifications;

namespace ShiftLedger.Api.Realtime;

// The SignalR leg of INotifier: pushes a just-persisted notification to the recipient's
// connected clients the moment it is raised.
public class SignalRPusher(IHubContext<NotificationsHub> hub) : IRealtimePusher
{
    public Task PushAsync(Guid recipientId, NotificationDto notification, CancellationToken cancellationToken) =>
        hub.Clients.User(recipientId.ToString())
            .SendAsync("NotificationCreated", notification, cancellationToken);

    // Rules N1/N2/N3: a SuperAdmin (org-wide group) and never in the same connection as a
    // department group (SuperAdmin has no department, DepartmentAdmin/Employee have no org-wide
    // membership — see NotificationsHub.OnConnectedAsync), so this never double-delivers.
    public async Task PushToDepartmentAsync(Guid departmentId, NotificationDto notification, CancellationToken cancellationToken)
    {
        await hub.Clients.Group(NotificationsHub.OrgWideGroup).SendAsync("NotificationCreated", notification, cancellationToken);
        await hub.Clients.Group(NotificationsHub.DepartmentGroup(departmentId)).SendAsync("NotificationCreated", notification, cancellationToken);
    }
}

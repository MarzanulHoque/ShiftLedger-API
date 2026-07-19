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
}

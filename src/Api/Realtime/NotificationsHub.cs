using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ShiftLedger.Api.Realtime;

// The in-app notification hub (docs/04 §2). Clients connect with their JWT (WebSockets pass it
// as ?access_token=…, wired in Program); pushes target Clients.User(<user id>), which SignalR
// resolves from the connection's NameIdentifier claim. No client->server methods in v1 —
// the REST endpoints remain the source of truth; this hub only delivers NotificationCreated.
[Authorize]
public class NotificationsHub : Hub;

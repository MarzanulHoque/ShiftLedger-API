using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Api.Realtime;

// The in-app notification hub (docs/04 §2). Clients connect with their JWT (WebSockets pass it
// as ?access_token=…, wired in Program); direct pushes target Clients.User(<user id>), which
// SignalR resolves from the connection's NameIdentifier claim. No client->server methods in v1 —
// the REST endpoints remain the source of truth; this hub only delivers NotificationCreated.
[Authorize]
public class NotificationsHub : Hub
{
    // Rules N1/N2/N3 (P11): the SuperAdmin cockpit's org-wide feed, and each department's own
    // feed — reused by P12's live activity stream, not just the bell.
    public const string OrgWideGroup = "org-wide";
    public static string DepartmentGroup(Guid departmentId) => $"dept-{departmentId}";

    // A connection joins exactly one of these groups, derived from the JWT's role/dept claims
    // (see JwtTokenService) — SuperAdmin has no "dept" claim, DepartmentAdmin/Employee always do.
    public override async Task OnConnectedAsync()
    {
        var roleClaim = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
        if (roleClaim == nameof(Role.SuperAdmin))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, OrgWideGroup);
        }
        else if (Guid.TryParse(Context.User?.FindFirst("dept")?.Value, out var departmentId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, DepartmentGroup(departmentId));
        }

        await base.OnConnectedAsync();
    }
}

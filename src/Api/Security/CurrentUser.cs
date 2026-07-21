using System.Security.Claims;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Api.Security;

// Reads the authenticated caller from the JWT on the current request. The token's `sub` claim
// (the user id) maps to ClaimTypes.NameIdentifier under the default JwtBearer inbound mapping;
// the role claim is emitted as ClaimTypes.Role (see Infrastructure/Security/JwtTokenService).
public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var sub = Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Principal?.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public bool IsSuperAdmin => Principal?.IsInRole(nameof(Role.SuperAdmin)) ?? false;

    public bool IsAdmin => IsSuperAdmin || (Principal?.IsInRole(nameof(Role.DepartmentAdmin)) ?? false);

    // Emitted as a plain "dept" claim by JwtTokenService (Rule RB2); absent/empty for SuperAdmin.
    public Guid? DepartmentId
    {
        get
        {
            var dept = Principal?.FindFirstValue("dept");
            return Guid.TryParse(dept, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
}

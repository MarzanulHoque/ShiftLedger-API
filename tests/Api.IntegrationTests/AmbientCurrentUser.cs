using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Api.IntegrationTests;

// EF Core compiles and caches the model (including HasQueryFilter closures) once per DbContext type,
// shared for the whole lifetime of the "Database" test collection (one IntegrationTestFixture, one
// DbContextOptions — see DbCollection). A filter that closes over a distinct ICurrentUser instance
// per test would only ever see whichever instance was captured at the FIRST model build; every later
// context — even one meant to represent a different user — would silently keep using that first
// snapshot. Production never hits this: ICurrentUser there is backed by IHttpContextAccessor, whose
// state is read fresh per request regardless of which wrapper instance captured it at model-build
// time. This mirrors that: one stable object identity for the model to close over, its answers
// mutated per CreateContext(...) call. Safe because "Database"-collection tests run sequentially,
// never with two differently-scoped contexts queried concurrently.
internal sealed class AmbientCurrentUser : ICurrentUser
{
    public Guid? UserId { get; private set; }
    public bool IsAdmin { get; private set; }
    public bool IsSuperAdmin { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public bool IsAuthenticated { get; private set; }

    public void Set(ICurrentUser? source)
    {
        UserId = source?.UserId;
        IsAdmin = source?.IsAdmin ?? false;
        IsSuperAdmin = source?.IsSuperAdmin ?? false;
        DepartmentId = source?.DepartmentId;
        IsAuthenticated = source?.IsAuthenticated ?? false;
    }
}

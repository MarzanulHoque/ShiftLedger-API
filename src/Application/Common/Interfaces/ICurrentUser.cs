namespace ShiftLedger.Application.Common.Interfaces;

// The authenticated caller as seen by the Application layer. Implemented in the API from the JWT
// claims (Api/Security/CurrentUser). Used for ownership scoping (Rule R2) and audit "who" (Rule A1).
public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAdmin { get; }
    bool IsAuthenticated { get; }
}

using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Application.Common.Options;
using ShiftLedger.Application.Common.Security;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Application.Auth;

// Adds a new hashed refresh token for the user and returns the raw token (given to the client once).
internal static class RefreshTokenIssuer
{
    public static string Issue(IAppDbContext db, Guid userId, JwtOptions options, DateTime nowUtc)
    {
        var raw = TokenHasher.NewRawToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = TokenHasher.Hash(raw),
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.AddDays(options.RefreshTokenDays),
        });
        return raw;
    }
}

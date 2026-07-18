using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Application.Common.Models;
using ShiftLedger.Application.Common.Options;
using ShiftLedger.Application.Common.Security;

namespace ShiftLedger.Application.Auth.Commands.RefreshToken;

public class RefreshTokenCommandHandler(
    IAppDbContext db,
    IJwtTokenService jwt,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : IRequestHandler<RefreshTokenCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var hash = TokenHasher.Hash(request.RefreshToken);

        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null || token.RevokedAtUtc is not null || token.ExpiresAtUtc <= now)
        {
            throw new InvalidCredentialsException("Invalid or expired refresh token.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new InvalidCredentialsException();
        }

        // Rotate: revoke the used token and issue a fresh one (replay of the old token is now detectable).
        var newRaw = RefreshTokenIssuer.Issue(db, user.Id, options.Value, now);
        token.RevokedAtUtc = now;
        token.ReplacedByTokenHash = TokenHasher.Hash(newRaw);

        var (accessToken, expiresAtUtc) = jwt.CreateAccessToken(user);
        await db.SaveChangesAsync(cancellationToken);

        return new AuthResult(accessToken, newRaw, expiresAtUtc);
    }
}

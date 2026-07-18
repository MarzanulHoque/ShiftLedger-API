using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Application.Common.Security;

namespace ShiftLedger.Application.Auth.Commands.ResetPassword;

public class ResetPasswordCommandHandler(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider) : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var hash = TokenHasher.Hash(request.Token);

        var token = await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null || token.UsedAtUtc is not null || token.ExpiresAtUtc <= now)
        {
            throw new InvalidCredentialsException("Invalid or expired reset token.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, cancellationToken)
            ?? throw new InvalidCredentialsException();

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.AccessFailedCount = 0;
        user.LockoutEndUtc = null;
        token.UsedAtUtc = now;

        // Revoke any active refresh tokens — a password reset invalidates existing sessions.
        var active = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var t in active)
        {
            t.RevokedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

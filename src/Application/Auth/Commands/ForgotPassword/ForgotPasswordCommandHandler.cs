using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Application.Common.Security;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Application.Auth.Commands.ForgotPassword;

public class ForgotPasswordCommandHandler(
    IAppDbContext db,
    TimeProvider timeProvider,
    ILogger<ForgotPasswordCommandHandler> logger) : IRequestHandler<ForgotPasswordCommand>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(60);

    public async Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive, cancellationToken);

        // Always succeed (no account enumeration). Only issue a token when the account exists.
        if (user is null) return;

        var raw = TokenHasher.NewRawToken();
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = TokenHasher.Hash(raw),
            ExpiresAtUtc = timeProvider.GetUtcNow().UtcDateTime.Add(TokenLifetime),
        });
        await db.SaveChangesAsync(cancellationToken);

        // v1 has no email delivery (post-MVP). Log the raw token so the reset flow is usable in dev.
        logger.LogInformation("Password reset token for {Email}: {Token}", email, raw);
    }
}

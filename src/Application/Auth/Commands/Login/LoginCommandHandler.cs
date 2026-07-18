using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Application.Common.Models;
using ShiftLedger.Application.Common.Options;

namespace ShiftLedger.Application.Auth.Commands.Login;

public class LoginCommandHandler(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwt,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : IRequestHandler<LoginCommand, AuthResult>
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<AuthResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Same generic error whether the user is missing or the password is wrong (no enumeration).
        if (user is null)
        {
            throw new InvalidCredentialsException();
        }

        if (user.LockoutEndUtc is { } lockoutEnd && lockoutEnd > now)
        {
            throw new InvalidCredentialsException("Account is temporarily locked. Try again later.");
        }

        if (!user.IsActive || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= MaxFailedAttempts)
            {
                user.LockoutEndUtc = now.Add(LockoutDuration);
                user.AccessFailedCount = 0;
            }
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidCredentialsException();
        }

        // Success — clear any prior failures.
        user.AccessFailedCount = 0;
        user.LockoutEndUtc = null;

        var (accessToken, expiresAtUtc) = jwt.CreateAccessToken(user);
        var refresh = RefreshTokenIssuer.Issue(db, user.Id, options.Value, now);
        await db.SaveChangesAsync(cancellationToken);

        return new AuthResult(accessToken, refresh, expiresAtUtc);
    }
}

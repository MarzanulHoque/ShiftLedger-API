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
    public async Task<AuthResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Same error whether the user is missing or the password is wrong (no account enumeration).
        if (user is null || !user.IsActive || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        var (accessToken, expiresAtUtc) = jwt.CreateAccessToken(user);
        var refresh = RefreshTokenIssuer.Issue(db, user.Id, options.Value, timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);

        return new AuthResult(accessToken, refresh, expiresAtUtc);
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShiftLedger.Application.Auth.Commands.Login;
using ShiftLedger.Application.Auth.Commands.RefreshToken;
using ShiftLedger.Application.Auth.Commands.ResetPassword;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Options;
using ShiftLedger.Application.Common.Security;
using ShiftLedger.Domain.Entities;
using ShiftLedger.Domain.Enums;
using ShiftLedger.Infrastructure.Persistence;
using ShiftLedger.Infrastructure.Security;
using Xunit;

namespace ShiftLedger.Api.IntegrationTests.Auth;

[Collection("Database")]
public class AuthFlowTests(IntegrationTestFixture fixture)
{
    private static readonly IOptions<JwtOptions> JwtOpts = Options.Create(new JwtOptions
    {
        Issuer = "t",
        Audience = "t",
        SigningKey = "test-signing-key-at-least-32-bytes-long-0000",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7,
    });
    private static readonly PasswordHasher Hasher = new();
    private static JwtTokenService Jwt => new(JwtOpts, TimeProvider.System);

    private static LoginCommandHandler LoginHandler(AppDbContext ctx) =>
        new(ctx, Hasher, Jwt, JwtOpts, TimeProvider.System);

    private static async Task<User> CreateUserAsync(AppDbContext ctx, string email, string password)
    {
        var user = new User { FullName = "Test", Email = email, PasswordHash = Hasher.Hash(password), Role = Role.Employee };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    [Fact] // R-rules groundwork: a wrong password is counted.
    public async Task Login_WrongPassword_IncrementsFailedCount()
    {
        const string email = "wrongpw@test.local";
        await using var ctx = fixture.CreateContext();
        await CreateUserAsync(ctx, email, "Correct#123");

        var act = async () => await LoginHandler(ctx).Handle(new LoginCommand(email, "bad"), default);
        await act.Should().ThrowAsync<InvalidCredentialsException>();

        await using var verify = fixture.CreateContext();
        (await verify.Users.FirstAsync(u => u.Email == email)).AccessFailedCount.Should().Be(1);
    }

    [Fact] // Lockout after repeated failures.
    public async Task Login_FiveFailures_LocksAccount()
    {
        const string email = "lockout@test.local";
        await using var ctx = fixture.CreateContext();
        await CreateUserAsync(ctx, email, "Correct#123");
        var handler = LoginHandler(ctx);

        for (var i = 0; i < 5; i++)
        {
            var bad = async () => await handler.Handle(new LoginCommand(email, "bad"), default);
            await bad.Should().ThrowAsync<InvalidCredentialsException>();
        }

        // The correct password is now rejected while the account is locked.
        var correct = async () => await handler.Handle(new LoginCommand(email, "Correct#123"), default);
        (await correct.Should().ThrowAsync<InvalidCredentialsException>()).Which.Message.Should().Contain("locked");

        await using var verify = fixture.CreateContext();
        (await verify.Users.FirstAsync(u => u.Email == email)).LockoutEndUtc.Should().NotBeNull();
    }

    [Fact] // Success issues tokens and clears prior failures.
    public async Task Login_Success_IssuesTokens_AndClearsFailures()
    {
        const string email = "success@test.local";
        await using var ctx = fixture.CreateContext();
        var user = await CreateUserAsync(ctx, email, "Correct#123");
        user.AccessFailedCount = 3;
        await ctx.SaveChangesAsync();

        var result = await LoginHandler(ctx).Handle(new LoginCommand(email, "Correct#123"), default);

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();

        await using var verify = fixture.CreateContext();
        (await verify.Users.FirstAsync(u => u.Email == email)).AccessFailedCount.Should().Be(0);
        (await verify.RefreshTokens.CountAsync(t => t.UserId == user.Id)).Should().Be(1);
    }

    [Fact] // Refresh rotates the token and rejects reuse of the old one.
    public async Task Refresh_RotatesToken_AndRejectsReuse()
    {
        const string email = "refresh@test.local";
        await using var ctx = fixture.CreateContext();
        await CreateUserAsync(ctx, email, "Correct#123");

        var first = await LoginHandler(ctx).Handle(new LoginCommand(email, "Correct#123"), default);
        var refreshHandler = new RefreshTokenCommandHandler(ctx, Jwt, JwtOpts, TimeProvider.System);
        var rotated = await refreshHandler.Handle(new RefreshTokenCommand(first.RefreshToken), default);

        rotated.RefreshToken.Should().NotBe(first.RefreshToken);

        var reuse = async () => await refreshHandler.Handle(new RefreshTokenCommand(first.RefreshToken), default);
        await reuse.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact] // Reset changes the password, consumes the token, and revokes active refresh tokens.
    public async Task Reset_ChangesPassword_ConsumesToken_AndRevokesRefreshTokens()
    {
        const string email = "reset@test.local";
        await using var ctx = fixture.CreateContext();
        var user = await CreateUserAsync(ctx, email, "OldPass#123");
        await LoginHandler(ctx).Handle(new LoginCommand(email, "OldPass#123"), default); // creates a refresh token

        var raw = TokenHasher.NewRawToken();
        ctx.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = TokenHasher.Hash(raw),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(60),
        });
        await ctx.SaveChangesAsync();

        await new ResetPasswordCommandHandler(ctx, Hasher, TimeProvider.System)
            .Handle(new ResetPasswordCommand(raw, "NewPass#456"), default);

        await using var verify = fixture.CreateContext();
        var updated = await verify.Users.FirstAsync(u => u.Email == email);
        Hasher.Verify("NewPass#456", updated.PasswordHash).Should().BeTrue();
        (await verify.PasswordResetTokens.FirstAsync(t => t.UserId == user.Id)).UsedAtUtc.Should().NotBeNull();
        (await verify.RefreshTokens.CountAsync(t => t.UserId == user.Id && t.RevokedAtUtc == null)).Should().Be(0);
    }
}

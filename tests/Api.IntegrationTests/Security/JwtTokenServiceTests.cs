using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Options;
using ShiftLedger.Application.Common.Options;
using ShiftLedger.Domain.Entities;
using ShiftLedger.Domain.Enums;
using ShiftLedger.Infrastructure.Security;
using Xunit;

namespace ShiftLedger.Api.IntegrationTests.Security;

// Pure unit test (no DB) — lives here because JwtTokenService is in the Infrastructure assembly.
public class JwtTokenServiceTests
{
    [Fact]
    public void CreateAccessToken_CarriesEmailRoleAndFutureExpiry()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "iss",
            Audience = "aud",
            SigningKey = "test-signing-key-at-least-32-bytes-long-0000",
            AccessTokenMinutes = 15,
        });
        var service = new JwtTokenService(options, TimeProvider.System);
        var user = new User { Email = "a@b.com", Role = Role.SuperAdmin };

        var (token, expiresAtUtc) = service.CreateAccessToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Issuer.Should().Be("iss");
        jwt.Claims.Should().Contain(c => c.Value == "a@b.com");
        jwt.Claims.Should().Contain(c => c.Value == "SuperAdmin");
        expiresAtUtc.Should().BeAfter(DateTime.UtcNow);
    }
}

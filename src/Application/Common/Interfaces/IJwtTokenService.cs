using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Application.Common.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user);
}

namespace ShiftLedger.Application.Common.Models;

public record AuthResult(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);

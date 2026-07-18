namespace ShiftLedger.Domain.Entities;

// Single-use, time-limited password-reset token. Stored hashed; consumed once (UsedAtUtc set).
public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
}

using ShiftLedger.Application.Common.Interfaces;
using IdentityHasher = Microsoft.AspNetCore.Identity.PasswordHasher<object>;
using Microsoft.AspNetCore.Identity;

namespace ShiftLedger.Infrastructure.Security;

// Wraps ASP.NET Core's PBKDF2 PasswordHasher. Only the hash is ever stored (never the raw password).
public class PasswordHasher : IPasswordHasher
{
    private static readonly object Dummy = new();
    private readonly IdentityHasher _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(Dummy, password);

    public bool Verify(string password, string passwordHash)
        => _hasher.VerifyHashedPassword(Dummy, passwordHash, password) != PasswordVerificationResult.Failed;
}

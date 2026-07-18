using System.Security.Cryptography;
using System.Text;

namespace ShiftLedger.Application.Common.Security;

// Refresh tokens are stored hashed. The raw token goes to the client; only its SHA-256 hash is persisted.
public static class TokenHasher
{
    public static string NewRawToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}

using System.Security.Cryptography;
using System.Text;

namespace VHSmart_Api.Shared.Infrastructure.Security;

// One-time activation token (C-02): the raw value is handed to the caller exactly once and
// only its SHA-256 hash is stored in AdmUserTokens (Database.md 5), so a database leak can
// never yield working tokens. Replaces the legacy "e-mail the password" defect (spec 23) -
// the token, never a password, travels out of band.
public static class OneTimeToken
{
    private const int DefaultExpiryHours = 48;

    // 32 random bytes = 256 bits of entropy; hex keeps the token URL-safe without padding.
    public static (string RawToken, string TokenHash, DateTime ExpiresAt) Issue(IConfiguration configuration)
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return (rawToken, Hash(rawToken), DateTime.UtcNow.AddHours(ExpiryHours(configuration)));
    }

    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    // Activation expiry is not in the spec; config wins when set (same style as Login:*).
    private static int ExpiryHours(IConfiguration configuration) =>
        int.TryParse(configuration["Activation:ExpiryHours"], out var hours) && hours > 0
            ? hours
            : DefaultExpiryHours;
}

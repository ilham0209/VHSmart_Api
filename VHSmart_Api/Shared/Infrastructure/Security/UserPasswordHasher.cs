using Microsoft.AspNetCore.Identity;
using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Shared.Infrastructure.Security;

// ASP.NET Core PasswordHasher (Database.md 5, CodingRules 1) in exactly one place so login
// verification, re-hashing and the platform-admin seed command can never disagree on the
// algorithm. The hasher is stateless; one instance is safe to share.
public static class UserPasswordHasher
{
    private static readonly PasswordHasher<UserEntity> Hasher = new();

    public static string Hash(UserEntity user, string password) =>
        Hasher.HashPassword(user, password);

    public static PasswordVerificationResult Verify(UserEntity user, string passwordHash, string password) =>
        Hasher.VerifyHashedPassword(user, passwordHash, password);
}

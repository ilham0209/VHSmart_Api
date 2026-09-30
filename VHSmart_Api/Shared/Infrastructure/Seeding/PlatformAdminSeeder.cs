using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Shared.Infrastructure.Seeding;

// Database.md 14: the first platform admin is created by an explicit seed command, never by
// a migration - the password must come from the secret store (D-29) and the operator picks
// it, so it can never end up in source control or in the model snapshot:
//
//   dotnet user-secrets set "Seed:PlatformAdminEmail"    "<login e-mail>"
//   dotnet user-secrets set "Seed:PlatformAdminName"     "<display name>"
//   dotnet user-secrets set "Seed:PlatformAdminPassword" "<password>"
//   dotnet run -- seed-platform-admin
//
// The command is idempotent: a second run reports the existing account and writes nothing.
public static class PlatformAdminSeeder
{
    public const string Command = "seed-platform-admin";

    public sealed record SeedResult(bool Success, string Message);

    public static async Task<SeedResult> RunAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var email = configuration["Seed:PlatformAdminEmail"]?.Trim();
        var name = configuration["Seed:PlatformAdminName"]?.Trim();
        var password = configuration["Seed:PlatformAdminPassword"];

        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(password))
        {
            return new SeedResult(
                false,
                "Seed:PlatformAdminEmail, Seed:PlatformAdminName and Seed:PlatformAdminPassword must be set. "
                + "Store the password in the secret store: "
                + "dotnet user-secrets set \"Seed:PlatformAdminPassword\" <value>");
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();

        // Global filter applies; compared case-insensitively like login does (spec 21.9).
        var existing = await db.Users
            .SingleOrDefaultAsync(user => user.Email.ToLower() == email.ToLower(), cancellationToken);

        if (existing is not null)
        {
            return existing.IsPlatformAdmin
                ? new SeedResult(true, $"Platform admin {email} already exists; nothing to do.")
                : new SeedResult(false, $"A user with e-mail {email} already exists and is not a platform admin.");
        }

        var user = new UserEntity
        {
            // Stored in upper case (Database.md 5).
            Name = name.ToUpperInvariant(),
            Email = email,
            IsActive = true,
            ActivatedAt = DateTime.UtcNow,
            // The operator just chose the password, so no forced change on first login.
            MustChangePassword = false,
            IsPlatformAdmin = true,
            RoleId = RoleSeedData.VhSmartAdminRoleId
        };
        user.PasswordHash = UserPasswordHasher.Hash(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return new SeedResult(true, $"Seeded platform admin {email}.");
    }
}

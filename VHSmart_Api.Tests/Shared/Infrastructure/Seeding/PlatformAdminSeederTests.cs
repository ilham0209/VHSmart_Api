using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Seeding;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Seeding;

public class PlatformAdminSeederTests
{
    private const string Email = "admin@vhsmart.local";

    private const string Password = "S3cret!Pass";

    [Fact]
    public async Task Run_MissingPasswordSecret_FailsWithoutWriting()
    {
        var services = await BuildServicesAsync(
            new Dictionary<string, string?>
            {
                ["Seed:PlatformAdminEmail"] = Email,
                ["Seed:PlatformAdminName"] = "Vh Smart Admin"
            });

        var result = await PlatformAdminSeeder.RunAsync(services);

        Assert.False(result.Success);
        Assert.Contains("Seed:PlatformAdminPassword", result.Message);
        Assert.Equal(0, await CountUsersAsync(services));
    }

    [Fact]
    public async Task Run_ValidSettings_CreatesTheFirstPlatformAdmin()
    {
        var services = await BuildServicesAsync(Secrets());

        var result = await PlatformAdminSeeder.RunAsync(services);

        Assert.True(result.Success);
        Assert.Contains(Email, result.Message);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var user = await db.Users.SingleAsync();

        Assert.True(user.IsPlatformAdmin);
        Assert.True(user.IsActive);
        Assert.NotNull(user.ActivatedAt);
        Assert.Equal("VH SMART ADMIN", user.Name);
        Assert.Equal(RoleSeedData.VhSmartAdminRoleId, user.RoleId);
        Assert.False(user.MustChangePassword);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Equal(
            PasswordVerificationResult.Success,
            UserPasswordHasher.Verify(user, user.PasswordHash, Password));
    }

    [Fact]
    public async Task Run_Twice_IsIdempotent()
    {
        var services = await BuildServicesAsync(Secrets());

        await PlatformAdminSeeder.RunAsync(services);
        var second = await PlatformAdminSeeder.RunAsync(services);

        Assert.True(second.Success);
        Assert.Contains("already exists", second.Message);
        Assert.Equal(1, await CountUsersAsync(services));
    }

    [Fact]
    public async Task Run_EmailBelongsToANonPlatformAdmin_Fails()
    {
        var services = await BuildServicesAsync(Secrets());
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
            var other = new UserEntity
            {
                Name = "OTHER USER",
                Email = Email,
                IsActive = true,
                RoleId = RoleSeedData.VhSmartAdminRoleId
            };
            other.PasswordHash = UserPasswordHasher.Hash(other, Password);
            db.Users.Add(other);
            await db.SaveChangesAsync();
        }

        var result = await PlatformAdminSeeder.RunAsync(services);

        Assert.False(result.Success);
        Assert.Contains("not a platform admin", result.Message);
        Assert.Equal(1, await CountUsersAsync(services));
    }

    [Fact]
    public async Task Run_SoftDeletedUserWithSameEmail_CreatesAFreshAccount()
    {
        var services = await BuildServicesAsync(Secrets());
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
            var deleted = new UserEntity
            {
                Name = "OLD USER",
                Email = Email,
                IsActive = false,
                RoleId = RoleSeedData.VhSmartAdminRoleId
            };
            deleted.PasswordHash = UserPasswordHasher.Hash(deleted, Password);
            db.Users.Add(deleted);
            await db.SaveChangesAsync();
            db.Users.Remove(deleted);
            await db.SaveChangesAsync();
        }

        var result = await PlatformAdminSeeder.RunAsync(services);

        // Uniqueness of e-mail counts non-deleted rows only (spec 21.9).
        Assert.True(result.Success);
        Assert.Equal(1, await CountUsersAsync(services));
        using var readScope = services.CreateScope();
        var readDb = readScope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var user = await readDb.Users.SingleAsync();
        Assert.True(user.IsPlatformAdmin);
        Assert.Equal(
            2,
            await readDb.Users.IgnoreQueryFilters().CountAsync());
    }

    private static Dictionary<string, string?> Secrets() =>
        new()
        {
            ["Seed:PlatformAdminEmail"] = Email,
            ["Seed:PlatformAdminName"] = "Vh Smart Admin",
            ["Seed:PlatformAdminPassword"] = Password
        };

    private static async Task<ServiceProvider> BuildServicesAsync(Dictionary<string, string?> settings)
    {
        // One name for the whole provider: the lambda must not mint a new database per context.
        var databaseName = TestDbFactory.NewDatabaseName();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddScoped<ICurrentUser, SystemCurrentUser>();
        services.AddDbContext<VHSmartDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<VHSmartDbContext>().Database.EnsureCreatedAsync();
        return provider;
    }

    private static async Task<int> CountUsersAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        return await db.Users.CountAsync();
    }
}



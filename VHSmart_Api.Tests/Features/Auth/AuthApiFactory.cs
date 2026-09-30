using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Auth;

// Boots the real pipeline - JWT bearer -> JwtCurrentUser -> AuthController -> MediatR - with
// SQL Server swapped for a private in-memory store. The signing key is generated per factory
// (D-29: no secret in source) and tokens are minted from the resolved JwtBearerOptions, so
// the switch-company tests authenticate with exactly what the app accepts.
internal sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly string _databaseName = $"VHSmartAuthApiTests-{Guid.NewGuid():N}";

    // Uploads must never land in the repository (CodingRules 13): every factory owns a private
    // storage root that is removed again when the factory is disposed.
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartApiTests", $"storage-{Guid.NewGuid():N}");

    public async Task<UserEntity> SeedUserAsync(
        string email = "admin@example.com",
        string password = "Passw0rd!",
        bool isActive = true,
        Guid? roleId = null,
        bool isPlatformAdmin = true)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        await db.Database.EnsureCreatedAsync();

        var user = new UserEntity
        {
            Name = "API TEST USER",
            Email = email,
            IsActive = isActive,
            IsPlatformAdmin = isPlatformAdmin,
            RoleId = roleId ?? RoleSeedData.VhSmartAdminRoleId,
            ActivatedAt = DateTime.UtcNow
        };
        user.PasswordHash = UserPasswordHasher.Hash(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    // A live role with no AdmRolePermissions rows: default deny (CodingRules 8.2) makes every
    // HasPermission check fail for it, which is how the 403 path is exercised.
    public async Task<RoleEntity> SeedRoleWithoutPermissionsAsync(string name = "PERMISSIONLESS ROLE")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        await db.Database.EnsureCreatedAsync();

        var role = new RoleEntity { Name = name, IsSystemRole = false };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role;
    }

    public async Task AddMembershipAsync(Guid userId, Guid companyId, bool isDefault)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        db.UserCompanies.Add(new UserCompanyEntity
        {
            UserId = userId,
            CompanyId = companyId,
            IsDefault = isDefault
        });
        await db.SaveChangesAsync();
    }

    public async Task LockAsync(Guid userId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var user = await db.Users.SingleAsync(x => x.Id == userId);
        user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
        await db.SaveChangesAsync();
    }

    public string CreateTokenFor(UserEntity user, Guid companyId)
    {
        var parameters = Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme)
            .TokenValidationParameters;

        var key = parameters.IssuerSigningKey as SymmetricSecurityKey
            ?? throw new InvalidOperationException("Jwt:SigningKey did not reach the test host.");

        var token = new JwtSecurityToken(
            issuer: parameters.ValidIssuer,
            audience: parameters.ValidAudience,
            claims:
            [
                new Claim(JwtClaims.UserId, user.Id.ToString()),
                new Claim(JwtClaims.CompanyId, companyId.ToString()),
                new Claim(JwtClaims.RoleId, user.RoleId.ToString()),
                new Claim(JwtClaims.IsPlatformAdmin, bool.TrueString),
                new Claim(JwtClaims.ViewAllCompanies, bool.TrueString)
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("FileStorage:RootPath", _storageRoot);

        builder.ConfigureTestServices(services =>
        {
            // Tests never touch a real database. Both EF registrations have to be removed
            // first, otherwise the SqlServer and the InMemory provider end up on one context.
            services.RemoveAll<IDbContextOptionsConfiguration<VHSmartDbContext>>();
            services.RemoveAll<DbContextOptions<VHSmartDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<VHSmartDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
    }
}

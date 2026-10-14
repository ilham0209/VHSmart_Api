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
using VHSmart_Api.Shared.Infrastructure.Storage;
using VHSmart_Api.Tests.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Reference;

// Real pipeline with an in-memory store, a private file-storage root (removed on dispose)
// and a permission stub over the Reference.View key. CompanyId is fixed per factory: the
// JWT carries it and the seeded rows carry it, so the tenant filter of a request sees its
// own data.
internal sealed class ReferenceApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly bool _grantPermissions;
    private readonly string _databaseName = $"VHSmartReferenceApiTests-{Guid.NewGuid():N}";
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartReferenceApiTests", $"storage-{Guid.NewGuid():N}");

    public ReferenceApiFactory(bool grantPermissions = true)
    {
        _grantPermissions = grantPermissions;
    }

    public Guid CompanyId { get; } = Guid.NewGuid();

    public string CreateToken()
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
                new Claim(JwtClaims.UserId, Guid.NewGuid().ToString()),
                new Claim(JwtClaims.CompanyId, CompanyId.ToString()),
                new Claim(JwtClaims.RoleId, RoleSeedData.VhSmartAdminRoleId.ToString()),
                new Claim(JwtClaims.IsPlatformAdmin, bool.FalseString),
                new Claim(JwtClaims.ViewAllCompanies, bool.FalseString)
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<Guid> SeedDatabaseAsync(string name = "Verify Halal")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        await db.Database.EnsureCreatedAsync();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var icon = await storage.SaveAsync(
            new MemoryStream([137, 80, 78, 71]), "icon.png", "image/png");
        var row = new WebLinkEntity
        {
            CompanyId = CompanyId,
            Name = name,
            Webpage = "https://verifyhalal.example.org",
            Description = "Halal verification portal",
            Icon = icon
        };
        db.WebLinks.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("FileStorage:RootPath", _storageRoot);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<VHSmartDbContext>>();
            services.RemoveAll<DbContextOptions<VHSmartDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<VHSmartDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            var granted = _grantPermissions
                ? new List<(Guid, string, PermissionAction)>
                {
                    (RoleSeedData.VhSmartAdminRoleId, PermissionKeys.ReferenceView, PermissionAction.View)
                }
                : [];
            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
    }
}

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
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Tests.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.RawMaterial.ManufacturerSupplier;

// Real pipeline with an in-memory store, a private file-storage root (removed on dispose) and
// a permission stub that grants all four RawMaterial.ManufacturerSupplier actions
// (View/Create/Edit/Delete). CompanyId is fixed per factory: the JWT carries it and the seeded
// rows carry it, so the tenant filter of a request sees its own data. No platform-admin claim -
// this screen is per-company reference data (spec 10.1).
internal sealed class ManufacturerSupplierApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly Guid _grantedRoleId;
    private readonly bool _grantPermissions;
    private readonly string _databaseName = $"VHSmartManufacturerSupplierApiTests-{Guid.NewGuid():N}";
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartManufacturerSupplierApiTests", $"storage-{Guid.NewGuid():N}");

    public ManufacturerSupplierApiFactory(Guid grantedRoleId, bool grantPermissions = true)
    {
        _grantedRoleId = grantedRoleId;
        _grantPermissions = grantPermissions;
    }

    public Guid CompanyId { get; } = Guid.NewGuid();

    public string CreateToken(Guid roleId)
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
                new Claim(JwtClaims.RoleId, roleId.ToString()),
                new Claim(JwtClaims.IsPlatformAdmin, bool.FalseString),
                new Claim(JwtClaims.ViewAllCompanies, bool.FalseString)
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task SeedDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    // Inserts a Manufacturer & Supplier directly under this factory's CompanyId (no HTTP
    // context in the seed scope, so the DbContext stamps "system"); CountryId defaults to the
    // seeded Malaysia row.
    public async Task<Guid> SeedRowAsync(
        string name,
        ManufacturerSupplierType type = ManufacturerSupplierType.Both,
        string? manufacturerEmail = null,
        string? supplierEmail = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var malaysiaId = await db.Countries
            .AsNoTracking()
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();

        var row = new ManufacturerSupplierEntity
        {
            CompanyId = CompanyId,
            Type = type,
            ManufacturerName = type == ManufacturerSupplierType.SupplierOnly ? null : name,
            ManufacturerAddress = type == ManufacturerSupplierType.SupplierOnly ? null : "Jalan Gombak 1",
            ManufacturerCountryId = type == ManufacturerSupplierType.SupplierOnly ? null : malaysiaId,
            ManufacturerEmail = type == ManufacturerSupplierType.SupplierOnly ? null : manufacturerEmail,
            SupplierName = type == ManufacturerSupplierType.ManufacturerOnly ? null : name,
            SupplierAddress = type == ManufacturerSupplierType.ManufacturerOnly ? null : "Jalan Gombak 2",
            SupplierCountryId = type == ManufacturerSupplierType.ManufacturerOnly ? null : malaysiaId,
            SupplierEmail = type == ManufacturerSupplierType.ManufacturerOnly ? null : supplierEmail
        };
        db.ManufacturerSuppliers.Add(row);
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

            var actions = new[] { PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Delete };
            var granted = _grantPermissions
                ? actions.Select(action => (_grantedRoleId, PermissionKeys.RawMaterialManufacturerSupplier, action)).ToArray()
                : Array.Empty<(Guid, string, PermissionAction)>();

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

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
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Tests.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Admin.Companies;

// Real pipeline with an in-memory store and a permission stub that grants the three actions
// the Manage Companies screen has (spec 6.3: view, edit, "+ add" - there is no delete). The
// token carries IsPlatformAdmin: every handler of this screen requires it (spec 6.3,
// Super-User only - D-07).
internal sealed class CompaniesApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly Guid _grantedRoleId;
    private readonly bool _grantPermissions;
    private readonly string _databaseName = $"VHSmartCompanyApiTests-{Guid.NewGuid():N}";
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartCompanyApiTests", $"storage-{Guid.NewGuid():N}");

    public CompaniesApiFactory(Guid grantedRoleId, bool grantPermissions = true)
    {
        _grantedRoleId = grantedRoleId;
        _grantPermissions = grantPermissions;
    }

    public string CreateToken(Guid roleId, bool isPlatformAdmin = true)
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
                new Claim(JwtClaims.CompanyId, Guid.NewGuid().ToString()),
                new Claim(JwtClaims.RoleId, roleId.ToString()),
                new Claim(JwtClaims.IsPlatformAdmin, isPlatformAdmin ? bool.TrueString : bool.FalseString),
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

    // Inserts a CB directly (no HTTP context in the seed scope, so the DbContext stamps
    // "system") and returns its id; the country defaults to the seeded Malaysia row.
    public async Task<Guid> SeedCertificationBodyAsync(string name = "Serunai CB")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new CertificationBodyEntity
        {
            Name = name,
            CountryId = await db.Countries
                .AsNoTracking()
                .Where(country => country.IsoCode == "MYS")
                .Select(country => country.Id)
                .SingleAsync(),
            City = "Kuala Lumpur",
            Postcode = "50000",
            State = "Wilayah Persekutuan",
            Telephone = "0380000000",
            Email = $"{Guid.NewGuid():N}@cb.example",
            ContactPerson = "Director"
        };
        db.CertificationBodies.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // D-20 source rows live in AdmGeneralData (COMPANY / Brand); C-01 only links them.
    public async Task<Guid> SeedBrandAsync(string name, Guid? companyId = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new GeneralDataEntity
        {
            CompanyId = companyId ?? Guid.NewGuid(),
            Group = GeneralDataGroup.COMPANY,
            Category = "Brand",
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public async Task<Guid> SeedCompanyAsync(
        string name,
        Guid? certificationBodyId = null,
        Guid[]? brandIds = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var company = new CompanyEntity
        {
            Name = name,
            CertificationBodyId = certificationBodyId ?? await SeedCertificationBodyAsync($"{name} CB"),
            RegistrationType = "Companies Commission Of Malaysia",
            BusinessRegistrationNo = $"BRN-{Guid.NewGuid():N}",
            OwnerStatus = "Muslim Owner",
            Address1 = "Jalan Perusahaan 1",
            Address2 = "Jalan Perusahaan 2",
            PostCode = "50000",
            District = "Gombak",
            State = "Selangor",
            CountryId = await db.Countries
                .AsNoTracking()
                .Where(country => country.IsoCode == "MYS")
                .Select(country => country.Id)
                .SingleAsync(),
            Telephone = "0300000000",
            Email = $"{Guid.NewGuid():N}@example.com"
        };
        db.Companies.Add(company);
        foreach (var brandId in brandIds ?? [])
            db.CompanyBrands.Add(new CompanyBrandEntity
            {
                CompanyId = company.Id,
                BrandId = brandId
            });
        await db.SaveChangesAsync();
        return company.Id;
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

            var actions = new[] { PermissionAction.View, PermissionAction.Create, PermissionAction.Edit };
            var granted = _grantPermissions
                ? actions.Select(action => (_grantedRoleId, PermissionKeys.AdminCompanies, action)).ToArray()
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

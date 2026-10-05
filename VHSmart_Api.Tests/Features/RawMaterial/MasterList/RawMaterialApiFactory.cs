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
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Tests.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.RawMaterial.MasterList;

// Real pipeline with an in-memory store and a permission stub that grants all four
// RawMaterial.MasterList actions (View/Create/Edit/Delete). CompanyId defaults to one fixed
// value per factory (the JWT carries it and the seeded rows carry it, so the special
// visibility filter of CodingRules 7.3 sees its own data) but a test may mint a token for
// another company to prove the shared rows are readable - and only readable.
internal sealed class RawMaterialApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly Guid _grantedRoleId;
    private readonly bool _grantPermissions;
    private readonly string _databaseName = $"VHSmartRawMaterialApiTests-{Guid.NewGuid():N}";

    public RawMaterialApiFactory(Guid grantedRoleId, bool grantPermissions = true)
    {
        _grantedRoleId = grantedRoleId;
        _grantPermissions = grantPermissions;
    }

    public Guid CompanyId { get; } = Guid.NewGuid();

    public string CreateToken(Guid roleId, Guid? companyId = null, bool viewAllCompanies = false)
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
                new Claim(JwtClaims.CompanyId, (companyId ?? CompanyId).ToString()),
                new Claim(JwtClaims.RoleId, roleId.ToString()),
                new Claim(JwtClaims.IsPlatformAdmin, bool.FalseString),
                new Claim(JwtClaims.ViewAllCompanies, viewAllCompanies ? bool.TrueString : bool.FalseString)
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

    // The two form dropdowns are PRODUCT General Data of the caller's company (spec 5.1).
    public async Task<Guid> SeedGeneralDataAsync(string category, string name, Guid? companyId = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new GeneralDataEntity
        {
            CompanyId = companyId ?? CompanyId,
            Group = GeneralDataGroup.PRODUCT,
            Category = category,
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public async Task<Guid> SeedManufacturerAsync(string name, Guid? companyId = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new ManufacturerSupplierEntity
        {
            CompanyId = companyId ?? CompanyId,
            Type = ManufacturerSupplierType.Both,
            ManufacturerName = name,
            ManufacturerAddress = "Jalan Gombak 1",
            ManufacturerCountryId = await db.Countries
                .AsNoTracking()
                .Where(country => country.IsoCode == "MYS")
                .Select(country => country.Id)
                .SingleAsync(),
            ManufacturerEmail = $"{Guid.NewGuid():N}@example.com"
        };
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A live company row, so the "Accessible For" picker has something to name and the
    // validator can find the ids a test sends.
    public async Task<Guid> SeedCompanyAsync(string name)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var company = new CompanyEntity
        {
            Name = name,
            CertificationBodyId = await SeedCertificationBodyAsync(db, $"{name} CB"),
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
        await db.SaveChangesAsync();
        return company.Id;
    }

    // Inserts a raw material directly (no HTTP context in the seed scope, so the DbContext
    // stamps "system"). companyId defaults to this factory's company: a test that needs a row
    // owned by somebody else passes its own id.
    public async Task<Guid> SeedRowAsync(
        string ingredient = "Rice Flour",
        string? ingredientCode = "RM-001",
        Guid? companyId = null,
        Guid? ingredientStatusId = null,
        Guid? ingredientSourceId = null,
        Guid? manufacturerId = null,
        Guid[]? accessibleCompanyIds = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();

        var row = new RawMaterialEntity
        {
            CompanyId = companyId ?? CompanyId,
            Category = RawMaterialCategory.Core,
            IngredientStatusId = ingredientStatusId
                ?? await SeedGeneralDataAsync("Ingredient Status", "Active"),
            Ingredient = ingredient,
            IngredientCode = ingredientCode,
            IngredientSourceId = ingredientSourceId
                ?? await SeedGeneralDataAsync("Ingredient Source", "Plant Based"),
            ManufacturerSupplierId = manufacturerId ?? await SeedManufacturerAsync("Santan Foods")
        };
        db.RawMaterials.Add(row);

        foreach (var accessibleCompanyId in accessibleCompanyIds ?? [])
            db.RawMaterialAccessibleCompanies.Add(new RawMaterialAccessibleCompanyEntity
            {
                RawMaterialId = row.Id,
                AccessibleCompanyId = accessibleCompanyId
            });

        await db.SaveChangesAsync();
        return row.Id;
    }

    private static async Task<Guid> SeedCertificationBodyAsync(VHSmartDbContext db, string name)
    {
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<VHSmartDbContext>>();
            services.RemoveAll<DbContextOptions<VHSmartDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<VHSmartDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            var actions = new[] { PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Delete };
            var granted = _grantPermissions
                ? actions.Select(action => (_grantedRoleId, PermissionKeys.RawMaterialMasterList, action)).ToArray()
                : Array.Empty<(Guid, string, PermissionAction)>();

            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }
}

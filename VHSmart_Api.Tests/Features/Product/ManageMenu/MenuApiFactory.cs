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
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Tests.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Product.ManageMenu;

// Real pipeline with an in-memory store and a permission stub that grants the
// Product.ManageMenu screen (all four actions). CompanyId defaults to one fixed value per
// factory (the JWT carries it and the seeded rows carry it, so the tenant filter sees its own
// data); a test may mint a token for another company to prove a foreign or shared menu stays
// read-only.
internal sealed class MenuApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly Guid _grantedRoleId;
    private readonly bool _grantPermissions;
    private readonly string _databaseName = $"VHSmartMenuApiTests-{Guid.NewGuid():N}";

    public MenuApiFactory(Guid grantedRoleId, bool grantPermissions = true)
    {
        _grantedRoleId = grantedRoleId;
        _grantPermissions = grantPermissions;
    }

    public Guid CompanyId { get; } = Guid.NewGuid();

    public string CreateToken(Guid roleId, Guid? companyId = null)
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
                new Claim(JwtClaims.ViewAllCompanies, bool.FalseString)
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // HasData seeds (schemes, roles, countries) only arrive with EnsureCreated, so every test
    // that needs data starts here.
    public async Task SeedDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    // The "Menu Category*" dropdown (spec 5.1 reference data is per company).
    public async Task<Guid> SeedMenuCategoryAsync(string name = "Permanent", Guid? companyId = null) =>
        await SeedGeneralDataAsync(GeneralDataGroup.COMPANY, "Menu Category", name, companyId);

    // A row of another dropdown, so the form's category source can be proven exclusive.
    public async Task<Guid> SeedBrandAsync(string name = "Sereni") =>
        await SeedGeneralDataAsync(GeneralDataGroup.COMPANY, "Brand", name, null);

    private async Task<Guid> SeedGeneralDataAsync(
        GeneralDataGroup group,
        string category,
        string name,
        Guid? companyId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new GeneralDataEntity
        {
            CompanyId = companyId ?? CompanyId,
            Group = group,
            Category = category,
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A live company row for the "List of Company" picker (spec 9.2): sharing names other
    // companies, so the list is not tenant scoped. The factory's own company gets a row with
    // its exact id so a token and its row line up.
    public async Task<Guid> SeedCompanyAsync(string name = "Sharing Partner", Guid? id = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();

        var certificationBody = new CertificationBodyEntity
        {
            Name = $"{name} CB",
            CountryId = await db.Countries.AsNoTracking()
                .Where(country => country.IsoCode == "MYS")
                .Select(country => country.Id)
                .SingleAsync(),
            City = "Kuala Lumpur",
            Postcode = "50000",
            State = "Wilayah Persekutuan"
        };
        db.CertificationBodies.Add(certificationBody);

        var company = new CompanyEntity
        {
            Name = name,
            CertificationBody = certificationBody,
            RegistrationType = "Companies Commission Of Malaysia",
            BusinessRegistrationNo = $"BRN-{Guid.NewGuid():N}",
            OwnerStatus = "Muslim Owner",
            Address1 = "Jalan Perusahaan 1",
            Address2 = "Jalan Perusahaan 2",
            PostCode = "50000",
            District = "Gombak",
            State = "Selangor",
            CountryId = certificationBody.CountryId,
            Telephone = "0300000000",
            Email = $"{Guid.NewGuid():N}@example.com"
        };
        if (id is not null)
            company.Id = id.Value;

        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company.Id;
    }

    // A raw material the caller can see (CodingRules 7.3), with the two PRODUCT dropdown rows
    // the Raw Material screen requires. The ingredient code is unique per company (D-17), so
    // it is generated rather than fixed.
    public async Task<Guid> SeedRawMaterialAsync(
        string ingredient = "Rice Flour",
        Guid? companyId = null,
        Guid[]? accessibleCompanyIds = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();

        var row = new RawMaterialEntity
        {
            CompanyId = companyId ?? CompanyId,
            Category = RawMaterialCategory.Core,
            IngredientStatusId = await SeedGeneralDataAsync(
                GeneralDataGroup.PRODUCT, "Ingredient Status", "Active", companyId),
            Ingredient = ingredient,
            IngredientCode = $"RM-{Guid.NewGuid():N}",
            ScientificName = "Oryza sativa",
            IngredientSourceId = await SeedGeneralDataAsync(
                GeneralDataGroup.PRODUCT, "Ingredient Source", "Plant Based", companyId),
            ManufacturerSupplierId = await SeedManufacturerAsync(companyId),
            IsPackagingMaterial = false
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

    private async Task<Guid> SeedManufacturerAsync(Guid? companyId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new ManufacturerSupplierEntity
        {
            CompanyId = companyId ?? CompanyId,
            Type = ManufacturerSupplierType.Both,
            ManufacturerName = "Santan Foods",
            ManufacturerAddress = "Jalan Gombak 1",
            ManufacturerContactNo = "0312345678",
            ManufacturerEmail = $"{Guid.NewGuid():N}@example.com"
        };
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A menu with its two child lists (spec 9.2): one company, one raw material and the
    // category dropdown row, all owned by this factory's company unless told otherwise.
    public async Task<Guid> SeedMenuAsync(
        string name = "Nasi Lemak",
        Guid? companyId = null,
        Guid? categoryId = null,
        Guid? accessibleCompanyId = null,
        Guid? rawMaterialId = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();

        var owner = companyId ?? CompanyId;
        var menu = new MenuEntity
        {
            CompanyId = owner,
            Name = name,
            CategoryId = categoryId ?? await SeedMenuCategoryAsync(),
            Description = "Everyday rice set",
            Status = MenuStatus.Active
        };
        db.Menus.Add(menu);

        db.MenuAccessibleCompanies.Add(new MenuAccessibleCompanyEntity
        {
            MenuId = menu.Id,
            AccessibleCompanyId = accessibleCompanyId ?? owner
        });

        db.MenuRawMaterials.Add(new MenuRawMaterialEntity
        {
            CompanyId = owner,
            MenuId = menu.Id,
            RawMaterialId = rawMaterialId ?? await SeedRawMaterialAsync()
        });

        await db.SaveChangesAsync();
        return menu.Id;
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
                ? actions.Select(action => (_grantedRoleId, PermissionKeys.ProductManageMenu, action)).ToArray()
                : Array.Empty<(Guid, string, PermissionAction)>();

            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }
}

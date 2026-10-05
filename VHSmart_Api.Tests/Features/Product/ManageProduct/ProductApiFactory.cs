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
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Tests.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Product.ManageProduct;

// Real pipeline with an in-memory store and a permission stub that grants all four
// Product.ManageProduct actions (View/Create/Edit/Delete). CompanyId defaults to one fixed
// value per factory (the JWT carries it and the seeded rows carry it, so the tenant filter
// sees its own data); a test may mint a token for another company to prove a foreign row
// stays invisible. No file endpoint exists yet, so the storage root is not needed.
internal sealed class ProductApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly Guid _grantedRoleId;
    private readonly bool _grantPermissions;
    private readonly string _databaseName = $"VHSmartProductApiTests-{Guid.NewGuid():N}";

    public ProductApiFactory(Guid grantedRoleId, bool grantPermissions = true)
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

    // HasData seeds (schemes, roles, countries) only arrive with EnsureCreated, so every
    // test that needs data starts here - same ritual as the raw material factory.
    public async Task SeedDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    // The form's dropdowns: own-company General Data rows (spec 9.1) and one manufacturer.
    public async Task<Guid> SeedBrandAsync(string name = "Sereni", Guid? companyId = null) =>
        await SeedGeneralDataAsync(GeneralDataGroup.COMPANY, "Brand", name, companyId);

    public async Task<Guid> SeedProductCategoryAsync(string name = "Sauces", Guid? companyId = null) =>
        await SeedGeneralDataAsync(GeneralDataGroup.PRODUCT, "Product Category", name, companyId);

    public async Task<Guid> SeedMarketingMethodAsync(string name = "Verify Halal", Guid? companyId = null) =>
        await SeedGeneralDataAsync(GeneralDataGroup.PRODUCT, "Marketing Method", name, companyId);

    public async Task<Guid> SeedManufacturerAsync(string name = "Santan Foods", Guid? companyId = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new ManufacturerSupplierEntity
        {
            CompanyId = companyId ?? CompanyId,
            Type = ManufacturerSupplierType.Both,
            ManufacturerName = name,
            ManufacturerAddress = "Jalan Gombak 1",
            ManufacturerEmail = $"{Guid.NewGuid():N}@example.com"
        };
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A supplier-only row: the form's Manufacturer dropdown and validator both reject it.
    public async Task<Guid> SeedSupplierOnlyAsync(Guid? companyId = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new ManufacturerSupplierEntity
        {
            CompanyId = companyId ?? CompanyId,
            Type = ManufacturerSupplierType.SupplierOnly,
            SupplierName = "Santan Supplies",
            SupplierAddress = "Jalan Gombak 2",
            SupplierEmail = $"{Guid.NewGuid():N}@example.com"
        };
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

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

    // Inserts a product directly (no HTTP context in the seed scope, so the DbContext
    // stamps "system"). companyId defaults to this factory's company: a test that needs a
    // row owned by somebody else passes its own id.
    public async Task<Guid> SeedRowAsync(
        string name = "Santan Kicap",
        string? code = "PRD-001",
        Guid? companyId = null,
        Guid? marketingMethodId = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new ProductEntity
        {
            CompanyId = companyId ?? CompanyId,
            SchemeId = await db.Schemes.AsNoTracking()
                .OrderBy(scheme => scheme.SortOrder)
                .Select(scheme => scheme.Id)
                .FirstAsync(),
            Name = name,
            ManufacturerSupplierId = await SeedManufacturerAsync(),
            BrandId = await SeedBrandAsync(),
            CategoryId = await SeedProductCategoryAsync(),
            Code = code,
            MarketingMethodId = marketingMethodId
        };
        db.Products.Add(row);
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
                ? actions.Select(action => (_grantedRoleId, PermissionKeys.ProductManageProduct, action)).ToArray()
                : Array.Empty<(Guid, string, PermissionAction)>();

            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }
}

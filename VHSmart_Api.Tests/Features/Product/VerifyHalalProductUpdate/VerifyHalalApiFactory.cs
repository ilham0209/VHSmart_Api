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

namespace VHSmart_Api.Tests.Features.Product.VerifyHalalProductUpdate;

// Real pipeline with an in-memory store and a permission stub that grants all four
// Product.VerifyHalalProductUpdate actions. CompanyId defaults to one fixed value per factory
// (the JWT carries it and the seeded rows carry it, so the tenant filter sees its own data);
// a test may mint a token for another company to prove a foreign row stays invisible.
internal sealed class VerifyHalalApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly Guid _grantedRoleId;
    private readonly bool _grantPermissions;
    private readonly string _databaseName = $"VHSmartVerifyHalalApiTests-{Guid.NewGuid():N}";

    public VerifyHalalApiFactory(Guid grantedRoleId, bool grantPermissions = true)
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

    public async Task SeedDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task<Guid> SeedBrandAsync(string name = "Sereni", Guid? companyId = null) =>
        await SeedGeneralDataAsync(GeneralDataGroup.COMPANY, "Brand", name, companyId);

    public async Task<Guid> SeedProductCategoryAsync(string name = "Sauces", Guid? companyId = null) =>
        await SeedGeneralDataAsync(GeneralDataGroup.PRODUCT, "Product Category", name, companyId);

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
    // stamps "system"). companyId defaults to this factory's company.
    public async Task<Guid> SeedRowAsync(
        string name = "Santan Kicap",
        Guid? companyId = null,
        string? publishStatus = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();

        var manufacturer = new ManufacturerSupplierEntity
        {
            CompanyId = companyId ?? CompanyId,
            Type = ManufacturerSupplierType.Both,
            ManufacturerName = "Santan Foods",
            ManufacturerAddress = "Jalan Gombak 1",
            ManufacturerEmail = $"{Guid.NewGuid():N}@example.com"
        };
        db.ManufacturerSuppliers.Add(manufacturer);

        var row = new ProductEntity
        {
            CompanyId = companyId ?? CompanyId,
            SchemeId = await db.Schemes.AsNoTracking()
                .OrderBy(scheme => scheme.SortOrder)
                .Select(scheme => scheme.Id)
                .FirstAsync(),
            Name = name,
            ManufacturerSupplierId = manufacturer.Id,
            BrandId = await SeedBrandAsync(companyId: companyId),
            CategoryId = await SeedProductCategoryAsync(companyId: companyId),
            Code = "PRD-001",
            VerifyHalalPublishStatus = publishStatus
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

            var actions = new[]
            {
                PermissionAction.View, PermissionAction.Create,
                PermissionAction.Edit, PermissionAction.Delete
            };
            var granted = _grantPermissions
                ? actions.Select(action =>
                    (_grantedRoleId, PermissionKeys.ProductVerifyHalalProductUpdate, action)).ToArray()
                : Array.Empty<(Guid, string, PermissionAction)>();

            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }
}

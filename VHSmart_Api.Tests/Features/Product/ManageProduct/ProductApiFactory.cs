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
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;
using VHSmart_Api.Tests.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Product.ManageProduct;

// Real pipeline with an in-memory store and a permission stub that grants all four
// Product.ManageProduct actions (View/Create/Edit/Delete). CompanyId defaults to one fixed
// value per factory (the JWT carries it and the seeded rows carry it, so the tenant filter
// sees its own data); a test may mint a token for another company to prove a foreign row
// stays invisible. File downloads are answered by an in-memory storage stub.
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

    // The tab's >= 1 brand rule (spec 6.3 / 9.1): the handlers ask for a ComCompanyBrands
    // link, so the General Data brand row on its own is not enough for the ingredient routes.
    public async Task<Guid> SeedCompanyBrandAsync(string name = "Sereni", Guid? companyId = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new CompanyBrandEntity
        {
            CompanyId = companyId ?? CompanyId,
            BrandId = await SeedBrandAsync(name, companyId)
        };
        db.CompanyBrands.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A raw material the caller can see (CodingRules 7.3), with the two PRODUCT dropdown rows
    // the RM screen requires. The ingredient code is unique per company (D-17), so it is
    // generated rather than fixed.
    public async Task<Guid> SeedRawMaterialAsync(string ingredient = "Rice Flour", Guid? companyId = null)
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
            IngredientSourceId = await SeedGeneralDataAsync(
                GeneralDataGroup.PRODUCT, "Ingredient Source", "Plant Based", companyId),
            ManufacturerSupplierId = await SeedManufacturerAsync(),
            IsPackagingMaterial = false
        };
        db.RawMaterials.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A row of the tab table (Database.md 9): unlink flips this status, it never deletes.
    public async Task<Guid> SeedIngredientAsync(
        Guid productId,
        Guid rawMaterialId,
        string mappingStatus = ProductIngredientMappingStatus.Active)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new ProductIngredientEntity
        {
            CompanyId = CompanyId,
            ProductId = productId,
            RawMaterialId = rawMaterialId,
            MappingStatus = mappingStatus
        };
        db.ProductIngredients.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // The per-company "HALAL CERTIFICATE" Supporting Document (R-06) plus one upload for the
    // material - the pair the certificate route and D-18 read.
    public async Task<Guid> SeedHalalCertificateAsync(
        Guid rawMaterialId,
        DateTime? expiryDate = null,
        string referenceNo = "JAKIM/1/0001",
        string authority = "JAKIM")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var documentType = new SupportingDocumentEntity
        {
            CompanyId = CompanyId,
            ForView = SupportingDocumentForView.RawMaterial,
            DocumentType = "HALAL CERTIFICATE",
            DocumentSequence = 1
        };
        db.SupportingDocuments.Add(documentType);

        var row = new RawMaterialAttachmentEntity
        {
            CompanyId = CompanyId,
            RawMaterialId = rawMaterialId,
            DocumentTypeId = documentType.Id,
            ExpiryDate = expiryDate,
            ReferenceNo = referenceNo,
            Authority = authority,
            Document = new StoredFile
            {
                FileName = "certificate.pdf",
                StorageKey = $"{Guid.NewGuid():N}",
                ContentType = "application/pdf",
                SizeBytes = 4
            }
        };
        db.RawMaterialAttachments.Add(row);
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

            // The ingredient tab downloads the HALAL CERTIFICATE, so the handler needs a
            // storage: no disk is touched in the test host.
            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage>(new StubFileStorage());

            var actions = new[] { PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Delete };
            var granted = _grantPermissions
                ? actions.Select(action => (_grantedRoleId, PermissionKeys.ProductManageProduct, action)).ToArray()
                : Array.Empty<(Guid, string, PermissionAction)>();

            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }
}

// Answers any storage key, so a download route can be exercised end to end without a
// storage root.
internal sealed class StubFileStorage : IFileStorage
{
    public Task<StoredFile> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream("certificate"u8.ToArray()));

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

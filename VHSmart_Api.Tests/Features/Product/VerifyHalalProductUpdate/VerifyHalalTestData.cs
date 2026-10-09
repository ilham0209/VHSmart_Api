using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Domain.RawMaterial;

namespace VHSmart_Api.Tests.Features.Product.VerifyHalalProductUpdate;

// Shared fixtures for the Verify Halal Product Update tests: two companies, their users, the
// View By dropdown's category rows and a ready-made product with a publish status. Every test
// works in its own in-memory database, so the shared company ids are safe to reuse. The scheme
// picker is HasData-seeded by the model (Database.md 14), so EnsureCreated already provides it.
internal static class VerifyHalalTestData
{
    public static readonly Guid CompanyA = Guid.NewGuid();

    public static readonly Guid CompanyB = Guid.NewGuid();

    public static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    public static TestCurrentUser UserB() => new(Guid.NewGuid().ToString(), CompanyB);

    public static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    public static async Task<Guid> FirstSchemeIdAsync(TestableVHSmartDbContext db) =>
        await db.Schemes.AsNoTracking()
            .OrderBy(row => row.SortOrder)
            .Select(row => row.Id)
            .FirstAsync();

    public static async Task<Guid> SeedGeneralDataAsync(
        TestableVHSmartDbContext db,
        GeneralDataGroup group,
        string category,
        string name,
        Guid? companyId = null)
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId ?? CompanyA,
            Group = group,
            Category = category,
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static Task<Guid> SeedBrandAsync(
        TestableVHSmartDbContext db,
        string name = "Sereni",
        Guid? companyId = null) =>
        SeedGeneralDataAsync(db, GeneralDataGroup.COMPANY, "Brand", name, companyId);

    public static Task<Guid> SeedProductCategoryAsync(
        TestableVHSmartDbContext db,
        string name = "Sauces",
        Guid? companyId = null) =>
        SeedGeneralDataAsync(db, GeneralDataGroup.PRODUCT, "Product Category", name, companyId);

    public static async Task<Guid> SeedManufacturerAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null,
        string name = "Santan Foods")
    {
        var row = new ManufacturerSupplierEntity
        {
            CompanyId = companyId ?? CompanyA,
            Type = ManufacturerSupplierType.Both,
            ManufacturerName = name,
            ManufacturerAddress = "Jalan Gombak 1",
            ManufacturerEmail = $"{Guid.NewGuid():N}@example.com"
        };
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedProductAsync(
        TestableVHSmartDbContext db,
        string name = "Santan Kicap",
        Guid? companyId = null,
        Guid? schemeId = null,
        Guid? manufacturerId = null,
        Guid? brandId = null,
        Guid? categoryId = null,
        string? code = "PRD-001",
        string? publishStatus = null)
    {
        var row = new ProductEntity
        {
            CompanyId = companyId ?? CompanyA,
            SchemeId = schemeId ?? await FirstSchemeIdAsync(db),
            Name = name,
            ManufacturerSupplierId = manufacturerId ?? await SeedManufacturerAsync(db, companyId),
            BrandId = brandId ?? await SeedBrandAsync(db, companyId: companyId),
            CategoryId = categoryId ?? await SeedProductCategoryAsync(db, companyId: companyId),
            Code = code,
            VerifyHalalPublishStatus = publishStatus
        };
        db.Products.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // The ComCompanyBrands link of spec 6.3 (D-20) plus a raw material and an ACTIVE ingredient
    // link - the triple ProductHalalInformation needs to derive an expiry date.
    public static async Task<(Guid ProductId, Guid RawMaterialId)> SeedProductWithIngredientAsync(
        TestableVHSmartDbContext db,
        string name = "Santan Kicap",
        Guid? companyId = null)
    {
        var productId = await SeedProductAsync(db, name, companyId);

        var rawMaterial = new RawMaterialEntity
        {
            CompanyId = companyId ?? CompanyA,
            Category = RawMaterialCategory.Core,
            IngredientStatusId = await SeedGeneralDataAsync(
                db, GeneralDataGroup.PRODUCT, "Ingredient Status", "Active", companyId),
            Ingredient = "Rice Flour",
            IngredientCode = $"RM-{Guid.NewGuid():N}",
            IngredientSourceId = await SeedGeneralDataAsync(
                db, GeneralDataGroup.PRODUCT, "Ingredient Source", "Plant Based", companyId),
            ManufacturerSupplierId = await SeedManufacturerAsync(db, companyId),
            IsPackagingMaterial = false
        };
        db.RawMaterials.Add(rawMaterial);

        db.ProductIngredients.Add(new ProductIngredientEntity
        {
            CompanyId = companyId ?? CompanyA,
            ProductId = productId,
            RawMaterialId = rawMaterial.Id,
            MappingStatus = ProductIngredientMappingStatus.Active
        });
        await db.SaveChangesAsync();

        return (productId, rawMaterial.Id);
    }

    // The per-company "HALAL CERTIFICATE" Supporting Document (R-06) plus one upload for the
    // material - the pair ProductHalalInformation reads for the expiry date.
    public static async Task<Guid> SeedHalalCertificateAsync(
        TestableVHSmartDbContext db,
        Guid rawMaterialId,
        DateTime? expiryDate = null,
        string referenceNo = "JAKIM/1/2026/0001",
        string authority = "JAKIM",
        Guid? companyId = null)
    {
        var documentType = new SupportingDocumentEntity
        {
            CompanyId = companyId ?? CompanyA,
            ForView = SupportingDocumentForView.RawMaterial,
            DocumentType = "HALAL CERTIFICATE",
            DocumentSequence = 1
        };
        db.SupportingDocuments.Add(documentType);

        var row = new RawMaterialAttachmentEntity
        {
            CompanyId = companyId ?? CompanyA,
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
}

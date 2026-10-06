using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Domain.RawMaterial;

namespace VHSmart_Api.Tests.Features.Product.ManageProduct;

// Shared fixtures for the Manage Product tests: two companies, their users, the form's
// dropdown rows (own-company General Data, a manufacturer, a supplier-only row) and a
// ready-made create command. Every test works in its own in-memory database, so the shared
// company ids are safe to reuse. The scheme picker is HasData-seeded by the model
// (Database.md 14), so EnsureCreated already provides it.
internal static class ProductTestData
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

    // The seeded scheme list (12.1): any live row satisfies the form's Product Scheme*.
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

    public static Task<Guid> SeedMarketingMethodAsync(
        TestableVHSmartDbContext db,
        string name = "Verify Halal",
        Guid? companyId = null) =>
        SeedGeneralDataAsync(db, GeneralDataGroup.PRODUCT, "Marketing Method", name, companyId);

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
            ManufacturerContactNo = "0312345678",
            ManufacturerEmail = $"{Guid.NewGuid():N}@example.com"
        };
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A supplier-only row: the Manufacturer dropdown offers no such row and the validator
    // rejects it, so the product form can never point at it.
    public static async Task<Guid> SeedSupplierOnlyAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null)
    {
        var row = new ManufacturerSupplierEntity
        {
            CompanyId = companyId ?? CompanyA,
            Type = ManufacturerSupplierType.SupplierOnly,
            SupplierName = "Santan Supplies",
            SupplierAddress = "Jalan Gombak 2",
            SupplierEmail = $"{Guid.NewGuid():N}@example.com"
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
        string? code = "PRD-001")
    {
        var row = new ProductEntity
        {
            CompanyId = companyId ?? CompanyA,
            SchemeId = schemeId ?? await FirstSchemeIdAsync(db),
            Name = name,
            ManufacturerSupplierId = manufacturerId ?? await SeedManufacturerAsync(db, companyId),
            BrandId = brandId ?? await SeedBrandAsync(db, companyId: companyId),
            CategoryId = categoryId ?? await SeedProductCategoryAsync(db, companyId: companyId),
            Code = code
        };
        db.Products.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // The ComCompanyBrands link of spec 6.3 (D-20). The Ingredient Information tab refuses to
    // answer without at least one of these - the General Data brand row alone is NOT enough.
    public static async Task<Guid> SeedCompanyBrandAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null,
        string name = "Sereni")
    {
        var row = new CompanyBrandEntity
        {
            CompanyId = companyId ?? CompanyA,
            BrandId = await SeedBrandAsync(db, name, companyId)
        };
        db.CompanyBrands.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A raw material the caller can see under the 7.3 filter, with the two PRODUCT dropdown
    // rows and a manufacturer the RM-02 form requires. The ingredient code is unique per
    // company (D-17), so it is generated rather than fixed.
    public static async Task<Guid> SeedRawMaterialAsync(
        TestableVHSmartDbContext db,
        string ingredient = "Rice Flour",
        Guid? companyId = null,
        Guid? manufacturerId = null,
        Guid[]? accessibleCompanyIds = null)
    {
        var row = new RawMaterialEntity
        {
            CompanyId = companyId ?? CompanyA,
            Category = RawMaterialCategory.Core,
            IngredientStatusId = await SeedGeneralDataAsync(
                db, GeneralDataGroup.PRODUCT, "Ingredient Status", "Active", companyId),
            Ingredient = ingredient,
            IngredientCode = $"RM-{Guid.NewGuid():N}",
            IngredientSourceId = await SeedGeneralDataAsync(
                db, GeneralDataGroup.PRODUCT, "Ingredient Source", "Plant Based", companyId),
            ManufacturerSupplierId = manufacturerId ?? await SeedManufacturerAsync(db, companyId),
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

    // A row of the tab table (Database.md 9). Nothing soft-deletes these: unlinking flips the
    // status, so a test seeds the status it wants to read.
    public static async Task<Guid> SeedIngredientAsync(
        TestableVHSmartDbContext db,
        Guid productId,
        Guid rawMaterialId,
        string mappingStatus = ProductIngredientMappingStatus.Active,
        Guid? companyId = null)
    {
        var row = new ProductIngredientEntity
        {
            CompanyId = companyId ?? CompanyA,
            ProductId = productId,
            RawMaterialId = rawMaterialId,
            MappingStatus = mappingStatus
        };
        db.ProductIngredients.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // The per-company "HALAL CERTIFICATE" Supporting Document (R-06) plus one upload for the
    // material - the pair the ingredient tab's certificate column and D-18 read.
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

    // A row of the image table (Database.md 9): isCurrent = false marks the superseded version
    // the tab never lists. The bytes are not written - a test that streams the picture uploads
    // through the real handler instead.
    public static async Task<Guid> SeedProductImageAsync(
        TestableVHSmartDbContext db,
        Guid productId,
        ProductImagePosition position,
        int version = 1,
        bool isCurrent = true,
        Guid? companyId = null,
        string fileName = "front.png")
    {
        var row = new ProductImageEntity
        {
            CompanyId = companyId ?? CompanyA,
            ProductId = productId,
            Position = position,
            Version = version,
            IsCurrent = isCurrent,
            Image = new StoredFile
            {
                FileName = fileName,
                StorageKey = $"{Guid.NewGuid():N}",
                ContentType = "image/png",
                SizeBytes = 4
            }
        };
        db.ProductImages.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<CreateProductCommand> CommandAsync(
        TestableVHSmartDbContext db,
        string? name = "Santan Kicap",
        Guid? schemeId = null,
        Guid? manufacturerId = null,
        Guid? brandId = null,
        Guid? categoryId = null,
        Guid? marketingMethodId = null,
        string? code = "PRD-001",
        string? gtin = "9551234567890") =>
        new CreateProductCommand(
            schemeId ?? await FirstSchemeIdAsync(db),
            name,
            manufacturerId ?? await SeedManufacturerAsync(db),
            brandId ?? await SeedBrandAsync(db),
            categoryId ?? await SeedProductCategoryAsync(db),
            code,
            gtin,
            "No MSG",
            "None",
            "120 kcal",
            "Malaysia",
            "250 ml",
            marketingMethodId);

    public static async Task<UpdateProductCommand> UpdateCommandAsync(
        TestableVHSmartDbContext db,
        Guid id,
        string? name = "Santan Kicap Updated",
        Guid? schemeId = null,
        Guid? manufacturerId = null,
        Guid? brandId = null,
        Guid? categoryId = null,
        Guid? marketingMethodId = null)
    {
        var command = await CommandAsync(db, name: name,
            schemeId: schemeId, manufacturerId: manufacturerId,
            brandId: brandId, categoryId: categoryId,
            marketingMethodId: marketingMethodId);
        return new UpdateProductCommand(
            id,
            command.SchemeId,
            command.Name,
            command.ManufacturerSupplierId,
            command.BrandId,
            command.CategoryId,
            command.Code,
            command.Gtin,
            command.NutritionContentClaims,
            command.PotentialAllergens,
            command.CalorieContent,
            command.AvailableAt,
            command.PackagingSize,
            command.MarketingMethodId);
    }
}

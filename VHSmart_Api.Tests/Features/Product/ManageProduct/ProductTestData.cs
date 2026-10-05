using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Domain.Admin;
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

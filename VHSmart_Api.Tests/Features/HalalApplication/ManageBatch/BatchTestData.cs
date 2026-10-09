using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Tests.Features.Premise.ManagePremise;
using VHSmart_Api.Tests.Features.Product.VerifyHalalProductUpdate;

namespace VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;

// Shared fixtures for the Manage Batch tests: two companies, their users, a product scheme
// and the seeded Food Premises scheme, plus the batch / premise / product rows the handlers
// read. Every test works in its own in-memory database, so the shared company ids are safe
// to reuse. Premises and D-18 products are seeded through the PR-02 / PD-06 fixtures so both
// features answer the same "complete" / "Valid" for the same rows.
internal static class BatchTestData
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

    // Any seeded scheme that is not Food Premises (Database.md 14 seeds nine, one flagged).
    public static async Task<Guid> ProductSchemeIdAsync(TestableVHSmartDbContext db) =>
        await db.Schemes.AsNoTracking()
            .Where(row => !row.IsFoodPremise)
            .OrderBy(row => row.SortOrder)
            .Select(row => row.Id)
            .FirstAsync();

    public static async Task<Guid> FoodPremiseSchemeIdAsync(TestableVHSmartDbContext db) =>
        await db.Schemes.AsNoTracking()
            .Where(row => row.IsFoodPremise)
            .Select(row => row.Id)
            .FirstAsync();

    public static async Task<Guid> SeedBrandAsync(
        TestableVHSmartDbContext db,
        string name = "Sereni",
        Guid? companyId = null)
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId ?? CompanyA,
            Group = GeneralDataGroup.COMPANY,
            Category = "Brand",
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

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

    // A row carrying ONLY the supplier half - not a manufacturer, so the batch manufacturer
    // dropdown must not offer it (spec 9.1 "Manufacturer*").
    public static async Task<Guid> SeedSupplierOnlyAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null,
        string name = "Santan Trading")
    {
        var row = new ManufacturerSupplierEntity
        {
            CompanyId = companyId ?? CompanyA,
            Type = ManufacturerSupplierType.SupplierOnly,
            SupplierName = name,
            SupplierAddress = "Jalan Gombak 2",
            SupplierEmail = $"{Guid.NewGuid():N}@example.com"
        };
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedBatchAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Santan Batch Pertama",
        Guid? schemeId = null,
        Guid? brandId = null,
        Guid? manufacturerId = null,
        string? cbReferenceNo = null,
        DateTime? submissionPlannedDate = null,
        string? description = null)
    {
        var row = new BatchEntity
        {
            CompanyId = companyId,
            SchemeId = schemeId ?? await ProductSchemeIdAsync(db),
            Name = name,
            BrandId = brandId ?? await SeedBrandAsync(db, companyId: companyId),
            ManufacturerSupplierId = manufacturerId,
            CbReferenceNo = cbReferenceNo,
            SubmissionPlannedDate = submissionPlannedDate,
            Description = description
        };
        db.Batches.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // D-15: all five required types uploaded with an unexpired date = COMPLETE DOCUMENTATION.
    public static async Task<Guid> SeedCompletePremiseAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Seri Rasa Factory",
        string? storeCode = null)
    {
        var premiseId = await PremiseTestData.SeedPremiseAsync(
            db, companyId, name: name, storeCode: storeCode);
        foreach (var documentType in PremiseDocumentStatusCalculator.RequiredDocumentTypes)
        {
            await PremiseTestData.SeedPremiseAttachmentAsync(
                db, companyId, premiseId, documentType, expiryDate: DateTime.UtcNow.AddDays(30));
        }

        return premiseId;
    }

    // A premise with no attachments at all = NOT COMPLETE DOCUMENTATION.
    public static Task<Guid> SeedIncompletePremiseAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Kedai Kopi Dummy") =>
        PremiseTestData.SeedPremiseAsync(db, companyId, name: name);

    // D-18 Valid: >= 1 ACTIVE ingredient whose raw material carries an unexpired HALAL
    // CERTIFICATE (the PD-06 fixtures seed the triple).
    public static async Task<Guid> SeedValidProductAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Santan Kicap")
    {
        var (productId, rawMaterialId) = await VerifyHalalTestData.SeedProductWithIngredientAsync(
            db, name, companyId);
        await VerifyHalalTestData.SeedHalalCertificateAsync(
            db, rawMaterialId, expiryDate: DateTime.UtcNow.AddDays(30), companyId: companyId);
        return productId;
    }

    public static async Task<Guid> SeedExpiredProductAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Santan Kicap Tengo")
    {
        var (productId, rawMaterialId) = await VerifyHalalTestData.SeedProductWithIngredientAsync(
            db, name, companyId);
        await VerifyHalalTestData.SeedHalalCertificateAsync(
            db, rawMaterialId, expiryDate: DateTime.UtcNow.AddDays(-30), companyId: companyId);
        return productId;
    }

    // A product with no linked ingredient at all - not Valid under D-18 either.
    public static Task<Guid> SeedUnlinkedProductAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Santan Tepung") =>
        VerifyHalalTestData.SeedProductAsync(db, name, companyId);

    public static async Task SeedBatchProductAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid batchId,
        Guid productId,
        string mappingStatus = BatchProductMappingStatus.Active)
    {
        db.BatchProducts.Add(new BatchProductEntity
        {
            CompanyId = companyId,
            BatchId = batchId,
            ProductId = productId,
            MappingStatus = mappingStatus
        });
        await db.SaveChangesAsync();
    }

    public static async Task<Guid> SeedBatchPremiseAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid batchId,
        Guid premiseId)
    {
        var row = new BatchPremiseEntity
        {
            CompanyId = companyId,
            BatchId = batchId,
            PremiseId = premiseId
        };
        db.BatchPremises.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }
}

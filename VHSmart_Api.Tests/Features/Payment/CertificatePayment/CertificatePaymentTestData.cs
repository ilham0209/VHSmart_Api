using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Domain.Product;

namespace VHSmart_Api.Tests.Features.Payment.CertificatePayment;

// Shared fixtures for the Payment > Certificate tests: direct seeders for schemes, batches,
// applications, staff, the brand product chain, payments and attachments. Every test works
// in its own in-memory database, so the shared company ids are safe to reuse.
internal static class CertificatePaymentTestData
{
    public static readonly Guid CompanyA = Guid.NewGuid();

    public static readonly Guid CompanyB = Guid.NewGuid();

    public static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    public static TestCurrentUser UserB() => new(Guid.NewGuid().ToString(), CompanyB);

    public static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        db.Companies.Add(new CompanyEntity { Id = CompanyA, Name = "HALAL CO" });
        db.Companies.Add(new CompanyEntity { Id = CompanyB, Name = "OTHER CO" });
        await db.SaveChangesAsync();
        return db;
    }

    // A CB row + the CompanyA link, so the detail / lookup CertificationBody read-through
    // resolves (the company's CB, Database.md note).
    public static async Task<Guid> SeedCertificationBodyAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null,
        string? name = null)
    {
        var row = new CertificationBodyEntity
        {
            Name = name ?? "JABATAN KEMAJUAN ISLAM MALAYSIA (JAKIM)"
        };
        db.CertificationBodies.Add(row);

        var company = await db.Companies
            .FirstAsync(row => row.Id == (companyId ?? CompanyA));
        company.CertificationBodyId = row.Id;
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedSchemeAsync(
        TestableVHSmartDbContext db,
        string? name = null)
    {
        var row = new SchemeEntity
        {
            Code = "FOOD",
            Name = name ?? "Halal Food",
            SortOrder = 1
        };
        db.Schemes.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedBatchAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid schemeId,
        string? name = null)
    {
        var row = new BatchEntity
        {
            CompanyId = companyId,
            SchemeId = schemeId,
            Name = name ?? "BATCH-2026"
        };
        db.Batches.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedApplicationAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid schemeId,
        Guid? batchId = null,
        string? referenceNo = null,
        string applicationType = "New",
        string status = ApplicationStatus.Draft,
        string? cbApplicationNo = null)
    {
        var row = new ApplicationEntity
        {
            CompanyId = companyId,
            ReferenceNo = referenceNo ?? $"HAL({schemeId:N})/12102026/1",
            ApplicationType = applicationType,
            Status = status,
            StatusDate = DateTime.UtcNow,
            SchemeId = schemeId,
            BatchId = batchId,
            CbApplicationNo = cbApplicationNo
        };
        db.Applications.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // The brand derivation chain of Database.md 12 ("brand is read from the linked
    // application"): brand GeneralData + a product category + one product + one ACTIVE
    // batch-product link. Returns the brand name for the assertion.
    public static async Task<string> SeedBrandAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid schemeId,
        Guid batchId,
        string brandName = "Seri Rasa")
    {
        var brand = new GeneralDataEntity
        {
            CompanyId = companyId,
            Group = GeneralDataGroup.PRODUCT,
            Category = "Brand",
            Name = brandName
        };
        var category = new GeneralDataEntity
        {
            CompanyId = companyId,
            Group = GeneralDataGroup.PRODUCT,
            Category = "Product Category",
            Name = "Sauce"
        };
        db.GeneralData.AddRange(brand, category);
        await db.SaveChangesAsync();

        var product = new ProductEntity
        {
            CompanyId = companyId,
            SchemeId = schemeId,
            Name = "Rendang Paste",
            BrandId = brand.Id,
            CategoryId = category.Id
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        db.BatchProducts.Add(new BatchProductEntity
        {
            CompanyId = companyId,
            BatchId = batchId,
            ProductId = product.Id,
            MappingStatus = BatchProductMappingStatus.Active
        });
        await db.SaveChangesAsync();
        return brandName;
    }

    public static async Task<Guid> SeedStaffAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string? name = null)
    {
        var row = new StaffEntity
        {
            CompanyId = companyId,
            Name = name ?? "Staff One",
            Email = $"{Guid.NewGuid():N}@example.com"
        };
        db.Staffs.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedPaymentAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid halalApplicationId,
        Guid submittedByStaffId,
        Guid receivedByStaffId,
        string status = PaymentStatus.RequestBankDraft,
        decimal amount = 500m,
        string? paymentVoucherNo = null)
    {
        var row = new CertificatePaymentEntity
        {
            CompanyId = companyId,
            HalalApplicationId = halalApplicationId,
            Amount = amount,
            Status = status,
            SubmittedByStaffId = submittedByStaffId,
            SubmittedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ReceivedByStaffId = receivedByStaffId,
            ReceivedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentVoucherNo = paymentVoucherNo
        };
        db.Payments.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedAttachmentAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid paymentId,
        string documentType = "Charge Letter",
        string fileName = "charge-letter.pdf")
    {
        var row = new PaymentAttachmentEntity
        {
            CompanyId = companyId,
            PaymentId = paymentId,
            DocumentType = documentType,
            Document = new StoredFile
            {
                FileName = fileName,
                StorageKey = $"{Guid.NewGuid():N}",
                ContentType = "application/pdf",
                SizeBytes = 128
            }
        };
        db.PaymentAttachments.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A ready payment with its scheme + application + staff (one shared staff row), for the
    // detail, workflow and attachment tests.
    public static async Task<(Guid SchemeId, Guid ApplicationId, Guid StaffId, Guid PaymentId)>
        SeedStackAsync(
            TestableVHSmartDbContext db,
            Guid companyId,
            string status = PaymentStatus.RequestBankDraft,
            decimal amount = 500m)
    {
        var schemeId = await SeedSchemeAsync(db);
        var applicationId = await SeedApplicationAsync(
            db, companyId, schemeId,
            referenceNo: $"HAL({schemeId:N})/12102026/1",
            cbApplicationNo: "CB-001");
        var staffId = await SeedStaffAsync(db, companyId);
        var paymentId = await SeedPaymentAsync(
            db, companyId, applicationId, staffId, staffId, status, amount);
        return (schemeId, applicationId, staffId, paymentId);
    }
}

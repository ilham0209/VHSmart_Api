using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.HalalApplication;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.CertificateItem;

// Fixtures shared by the Certificate Item and Halal Certificate tests: certificate rows and
// item rows over the My Application fixture set (same companies, same application seeder),
// plus a submitted application with a batch for the approval-hook tests. Every test works
// in its own in-memory database, so the shared company ids are safe to reuse.
internal static class CertificateTestData
{
    public static async Task<Guid> SeedCertificateAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid applicationId,
        string certificateNo = "HAL-2026-0001",
        DateOnly? issuedDate = null,
        DateOnly? expiryDate = null,
        StoredFile? document = null)
    {
        var row = new HalalCertificateEntity
        {
            CompanyId = companyId,
            ApplicationId = applicationId,
            CertificateNo = certificateNo,
            IssuedDate = issuedDate,
            ExpiryDate = expiryDate,
            Document = document
        };
        db.HalalCertificates.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedCertificateItemAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid applicationId,
        string itemName = "Santan Kicap",
        Guid? productId = null,
        Guid? premiseId = null,
        Guid? brandId = null,
        Guid? certificateId = null)
    {
        var row = new CertificateItemEntity
        {
            CompanyId = companyId,
            ApplicationId = applicationId,
            ItemName = itemName,
            ProductId = productId,
            PremiseId = premiseId,
            BrandId = brandId,
            HalalCertificateId = certificateId
        };
        db.CertificateItems.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A submitted (PROCESSING) application carrying a batch - the state right before
    // "Tagging Application Status" moves it to APPLICATION APPROVED.
    public static async Task<(Guid applicationId, Guid batchId)>
        SeedSubmittedApplicationWithBatchAsync(TestableVHSmartDbContext db, Guid companyId)
    {
        var batchId = await ManageBatch.BatchTestData.SeedBatchAsync(db, companyId);
        var applicationId = await SeedApplicationAsync(
            db,
            companyId,
            status: "PROCESSING AT JAKIM (NEW)",
            batchId: batchId);
        return (applicationId, batchId);
    }
}

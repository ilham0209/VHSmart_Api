using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.MyApplication;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Tests.Features.Product.VerifyHalalProductUpdate;
using static VHSmart_Api.Tests.Features.HalalApplication.CertificateItem.CertificateTestData;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.CertificateItem;

// Database.md 10: AppCertificateItems are "created when the app becomes approved". The only
// path to APPLICATION APPROVED is "Tagging Application Status", so these tests drive the
// tagging handler and assert the snapshot: ACTIVE batch products, linked batch premises,
// skip-ahead across the threshold and idempotence on re-tags.
public class CertificateApprovalItemTests
{
    private static Task<ApplicationStatusTagResponse> TagAsync(
        TestableVHSmartDbContext db,
        TestCurrentUser user,
        Guid applicationId,
        string status) =>
        new TagApplicationStatusHandler(db, user)
            .Handle(
                new TagApplicationStatusCommand(applicationId, status, null),
                CancellationToken.None);

    [Fact]
    public async Task TagToApproved_SnapshotsTheActiveBatchProducts()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var (applicationId, batchId) = await SeedSubmittedApplicationWithBatchAsync(db, CompanyA);
        var brandId = await BatchTestData.SeedBrandAsync(db, name: "Sereni", companyId: CompanyA);
        var activeProductId = await VerifyHalalTestData.SeedProductAsync(
            db, "Santan Kicap", CompanyA, brandId: brandId);
        var inactiveProductId = await VerifyHalalTestData.SeedProductAsync(
            db, "Santan Tepung", CompanyA, brandId: brandId);
        await BatchTestData.SeedBatchProductAsync(db, CompanyA, batchId, activeProductId);
        await BatchTestData.SeedBatchProductAsync(
            db, CompanyA, batchId, inactiveProductId,
            mappingStatus: BatchProductMappingStatus.Inactive);

        await TagAsync(db, user, applicationId, ApplicationStatus.ApplicationApproved);

        var item = Assert.Single(await db.CertificateItems.AsNoTracking().ToListAsync());
        Assert.Equal(activeProductId, item.ProductId);
        Assert.Equal("Santan Kicap", item.ItemName);
        Assert.Equal(brandId, item.BrandId);
        Assert.Null(item.PremiseId);
        Assert.Null(item.HalalCertificateId);
        Assert.Equal(applicationId, item.ApplicationId);
    }

    [Fact]
    public async Task TagSkipAheadPastApproved_StillCreatesTheItems()
    {
        // D-26 lets the dialog jump to any later status - the approval threshold is still
        // crossed, so the snapshot happens (flagged stance).
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var (applicationId, batchId) = await SeedSubmittedApplicationWithBatchAsync(db, CompanyA);
        var productId = await VerifyHalalTestData.SeedProductAsync(
            db, "Santan Kicap", CompanyA);
        await BatchTestData.SeedBatchProductAsync(db, CompanyA, batchId, productId);

        await TagAsync(db, user, applicationId, ApplicationStatus.AuditCompleted);

        Assert.Equal(
            ApplicationStatus.AuditCompleted,
            (await db.Applications.AsNoTracking().SingleAsync()).Status);
        var item = Assert.Single(await db.CertificateItems.AsNoTracking().ToListAsync());
        Assert.Equal(productId, item.ProductId);
    }

    [Fact]
    public async Task RetaggingTheApprovedStatus_DoesNotDuplicateTheItems()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var (applicationId, batchId) = await SeedSubmittedApplicationWithBatchAsync(db, CompanyA);
        var firstProductId = await VerifyHalalTestData.SeedProductAsync(
            db, "Santan Kicap", CompanyA);
        var secondProductId = await VerifyHalalTestData.SeedProductAsync(
            db, "Santan Tepung", CompanyA);
        await BatchTestData.SeedBatchProductAsync(db, CompanyA, batchId, firstProductId);
        await BatchTestData.SeedBatchProductAsync(db, CompanyA, batchId, secondProductId);

        await TagAsync(db, user, applicationId, ApplicationStatus.ApplicationApproved);
        await TagAsync(db, user, applicationId, ApplicationStatus.ApplicationApproved);
        await TagAsync(db, user, applicationId, ApplicationStatus.AuditInProgress);

        Assert.Equal(2, await db.CertificateItems.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task TagToApproved_SnapshotsTheLinkedPremises()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var batchId = await BatchTestData.SeedBatchAsync(
            db, CompanyA, schemeId: await BatchTestData.FoodPremiseSchemeIdAsync(db));
        var applicationId = await SeedApplicationAsync(
            db, CompanyA, status: "PROCESSING AT JAKIM (NEW)", batchId: batchId);
        var premiseId = await BatchTestData.SeedCompletePremiseAsync(
            db, CompanyA, name: "Seri Rasa Factory");
        await BatchTestData.SeedBatchPremiseAsync(db, CompanyA, batchId, premiseId);

        await TagAsync(db, user, applicationId, ApplicationStatus.ApplicationApproved);

        var item = Assert.Single(await db.CertificateItems.AsNoTracking().ToListAsync());
        Assert.Equal(premiseId, item.PremiseId);
        Assert.Null(item.ProductId);
        Assert.Equal("Seri Rasa Factory", item.ItemName);
        // The premise itself has no brand: the batch's brand is what the application was
        // filed under (flagged stance).
        var batchBrandId = await db.Batches.AsNoTracking()
            .Where(row => row.Id == batchId)
            .Select(row => row.BrandId)
            .SingleAsync();
        Assert.Equal(batchBrandId, item.BrandId);
    }

    [Fact]
    public async Task TagToApproved_WithoutABatch_CreatesNoItems()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(
            db, CompanyA, status: "PROCESSING AT JAKIM (NEW)");

        await TagAsync(db, user, applicationId, ApplicationStatus.ApplicationApproved);

        Assert.Equal(0, await db.CertificateItems.CountAsync());
        Assert.Equal(
            ApplicationStatus.ApplicationApproved,
            (await db.Applications.AsNoTracking().SingleAsync()).Status);
    }
}

using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Tests.Features.HalalApplication.CertificateItem;
using VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

namespace VHSmart_Api.Tests.Features.Premise.ManagePremise;

// Halal Information tab (spec 7.7 [CONFIRMED]): one row per application of the premise
// (premise -> AppBatchPremises -> batch -> application), newest StatusDate first, with the
// scheme / CB reference joins and the exactly-one certificate rule (Q17) over D-04.
public class PremiseHalalInformationTests
{
    private static async Task<(Guid PremiseId, Guid BatchId)> SeedPremiseInBatchAsync(
        TestableVHSmartDbContext db,
        Guid companyId)
    {
        var schemeId = await BatchTestData.FoodPremiseSchemeIdAsync(db);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, companyId);
        var batchId = await BatchTestData.SeedBatchAsync(
            db, companyId, schemeId: schemeId, cbReferenceNo: "CB-07");
        await BatchTestData.SeedBatchPremiseAsync(db, companyId, batchId, premiseId);
        return (premiseId, batchId);
    }

    private static async Task<Guid> SeedApplicationAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid batchId,
        string referenceNo,
        DateTime statusDate)
    {
        var schemeId = await BatchTestData.FoodPremiseSchemeIdAsync(db);
        return await ApplicationTestData.SeedApplicationAsync(
            db,
            companyId,
            referenceNo: referenceNo,
            schemeId: schemeId,
            batchId: batchId,
            status: ApplicationStatus.ApplicationApproved,
            statusDate: statusDate);
    }

    [Fact]
    public async Task Handle_PremiseWithApplications_ReturnsRowsNewestFirstWithJoins()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var (premiseId, batchId) = await SeedPremiseInBatchAsync(db, user.CompanyId);
        var schemeId = await BatchTestData.FoodPremiseSchemeIdAsync(db);
        var newerId = await SeedApplicationAsync(
            db, user.CompanyId, batchId, "VHS-NEWER", new DateTime(2026, 3, 1));
        await SeedApplicationAsync(
            db, user.CompanyId, batchId, "VHS-OLDER", new DateTime(2026, 1, 1));
        await CertificateTestData.SeedCertificateAsync(
            db, user.CompanyId, newerId,
            certificateNo: "HAL-2026-0001", expiryDate: new DateOnly(2027, 6, 30));

        var rows = await new GetPremiseHalalInformationHandler(db, user)
            .Handle(new GetPremiseHalalInformationQuery(premiseId), CancellationToken.None);

        Assert.Equal(2, rows.Count);
        var newest = rows[0];
        Assert.Equal(newerId, newest.ApplicationId);
        Assert.Equal("VHS-NEWER", newest.VhSmartReferenceNo);
        Assert.Equal("CB-07", newest.CbReferenceNo);
        Assert.Equal(
            await db.Schemes
                .Where(row => row.Id == schemeId)
                .Select(row => row.Name)
                .SingleAsync(),
            newest.Scheme);
        Assert.Equal(ApplicationStatus.ApplicationApproved, newest.ApplicationStatus);
        Assert.Equal("HAL-2026-0001", newest.HalalCertificateNo);
        Assert.Equal(HalalStatus.Valid, newest.HalalCertificateStatus);
        Assert.Equal(new DateOnly(2027, 6, 30), newest.HalalExpiryDate);
        Assert.Equal("VHS-OLDER", rows[1].VhSmartReferenceNo);
        Assert.Null(rows[1].HalalCertificateNo);
        Assert.Null(rows[1].HalalCertificateStatus);
        Assert.Null(rows[1].HalalExpiryDate);
    }

    [Fact]
    public async Task Handle_ExpiredOrNoExpiryCertificate_FollowsD04()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var (premiseId, batchId) = await SeedPremiseInBatchAsync(db, user.CompanyId);
        var expiredApp = await SeedApplicationAsync(
            db, user.CompanyId, batchId, "VHS-EXPIRED", new DateTime(2026, 3, 1));
        var openApp = await SeedApplicationAsync(
            db, user.CompanyId, batchId, "VHS-OPEN", new DateTime(2026, 1, 1));
        await CertificateTestData.SeedCertificateAsync(
            db, user.CompanyId, expiredApp,
            certificateNo: "HAL-EXPIRED", expiryDate: new DateOnly(2020, 1, 1));
        await CertificateTestData.SeedCertificateAsync(
            db, user.CompanyId, openApp,
            certificateNo: "HAL-OPEN", expiryDate: null);

        var rows = await new GetPremiseHalalInformationHandler(db, user)
            .Handle(new GetPremiseHalalInformationQuery(premiseId), CancellationToken.None);

        var expired = Assert.Single(rows, row => row.ApplicationId == expiredApp);
        Assert.Equal(HalalStatus.Expired, expired.HalalCertificateStatus);
        Assert.Equal(new DateOnly(2020, 1, 1), expired.HalalExpiryDate);
        var open = Assert.Single(rows, row => row.ApplicationId == openApp);
        Assert.Equal(HalalStatus.Valid, open.HalalCertificateStatus);
        Assert.Null(open.HalalExpiryDate);
    }

    [Fact]
    public async Task Handle_MultipleCertificates_LeavesCertificateColumnsEmpty()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var (premiseId, batchId) = await SeedPremiseInBatchAsync(db, user.CompanyId);
        var applicationId = await SeedApplicationAsync(
            db, user.CompanyId, batchId, "VHS-MULTI", new DateTime(2026, 3, 1));
        await CertificateTestData.SeedCertificateAsync(
            db, user.CompanyId, applicationId, certificateNo: "HAL-A");
        await CertificateTestData.SeedCertificateAsync(
            db, user.CompanyId, applicationId, certificateNo: "HAL-B");

        var rows = await new GetPremiseHalalInformationHandler(db, user)
            .Handle(new GetPremiseHalalInformationQuery(premiseId), CancellationToken.None);

        // Q17: with several certificates the spec never says which one the cell shows, so
        // the row stays but the certificate columns stay empty.
        var row = Assert.Single(rows);
        Assert.Equal("VHS-MULTI", row.VhSmartReferenceNo);
        Assert.Null(row.HalalCertificateNo);
        Assert.Null(row.HalalCertificateStatus);
        Assert.Null(row.HalalExpiryDate);
    }

    [Fact]
    public async Task Handle_PremiseNeverAssociatedToABatch_ReturnsEmpty()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        var batchId = await BatchTestData.SeedBatchAsync(db, user.CompanyId);
        await ApplicationTestData.SeedApplicationAsync(
            db, user.CompanyId, batchId: batchId);

        var rows = await new GetPremiseHalalInformationHandler(db, user)
            .Handle(new GetPremiseHalalInformationQuery(premiseId), CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Handle_UnknownOrForeignPremise_ThrowsNotFound()
    {
        var user = PremiseTestData.CompanyUser();
        var otherCompany = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var foreignPremiseId = await PremiseTestData.SeedPremiseAsync(
            db, otherCompany.CompanyId, "Foreign premise");

        var unknown = await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetPremiseHalalInformationHandler(db, user).Handle(
                new GetPremiseHalalInformationQuery(Guid.NewGuid()), CancellationToken.None));
        var foreign = await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetPremiseHalalInformationHandler(db, user).Handle(
                new GetPremiseHalalInformationQuery(foreignPremiseId), CancellationToken.None));

        Assert.Equal("Premise not found.", unknown.Message);
        Assert.Equal("Premise not found.", foreign.Message);
    }
}

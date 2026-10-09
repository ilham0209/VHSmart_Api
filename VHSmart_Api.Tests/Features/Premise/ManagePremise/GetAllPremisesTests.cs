using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Models;
using VHSmart_Api.Tests.Features.HalalApplication.CertificateItem;
using VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

namespace VHSmart_Api.Tests.Features.Premise.ManagePremise;

public class GetAllPremisesTests
{
    [Fact]
    public async Task Handle_DefaultSort_IsStoreNameAscendingWithJoins()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, "Verify Halal Sdn Bhd", user.CompanyId);
        var managerId = await PremiseTestData.SeedStaffAsync(db, user.CompanyId, "Ahmad Razali");
        await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, name: "Zeta Depot",
            storeCode: "SC-90", areaManagerStaffId: managerId, address3: "Lot 3");
        await PremiseTestData.SeedPremiseAsync(db, user.CompanyId, name: "Alpha Kitchen");

        var grid = await new GetAllPremisesHandler(db, user)
            .Handle(new GetAllPremisesQuery(), CancellationToken.None);

        var rows = grid.Data.ToList();
        Assert.Equal(2, grid.TotalRecords);
        Assert.Equal(new[] { "Alpha Kitchen", "Zeta Depot" }, rows.Select(row => row.StoreName));
        // Address composes line 1 + line 2 + optional line 3, trimmed (spec 7.7 list column).
        Assert.Equal("1 Jalan Verify Taman Industri", rows[0].Address);
        Assert.Equal("1 Jalan Verify Taman Industri Lot 3", rows[1].Address);
        Assert.Equal("Ahmad Razali", rows[1].AreaManager);
        Assert.Equal("SC-90", rows[1].StoreCode);
        Assert.Equal("Shah Alam", rows[0].City);
        Assert.Equal("Selangor", rows[0].State);
        Assert.Equal(
            await db.Countries
                .Where(c => c.IsoCode == "MYS")
                .Select(c => c.Name)
                .SingleAsync(),
            rows[0].Country);
        Assert.Equal(PremiseType.Factory, rows[0].PremiseType);
    }

    [Fact]
    public async Task Handle_PremiseTypeFilter_ReturnsOnlyMatchingRows()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, name: "Cold Store", premiseType: PremiseType.ColdRoomAndWarehouse);
        await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, name: "Rosa Cafe", premiseType: PremiseType.RestaurantsAndCafe);

        var all = await new GetAllPremisesHandler(db, user)
            .Handle(new GetAllPremisesQuery(), CancellationToken.None);
        var onlyCafe = await new GetAllPremisesHandler(db, user)
            .Handle(new GetAllPremisesQuery { PremiseType = PremiseType.RestaurantsAndCafe },
                CancellationToken.None);

        Assert.Equal(2, all.TotalRecords);
        Assert.Equal("Rosa Cafe", Assert.Single(onlyCafe.Data).StoreName);
    }

    [Fact]
    public async Task Handle_SearchTerm_FindsStoreCodeNameCityOrEmail()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, name: "Main Factory", storeCode: "SC-01");
        await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, name: "Branch Kitchen", city: "Penang");

        var byStoreCode = await new GetAllPremisesHandler(db, user)
            .Handle(new GetAllPremisesQuery
            {
                Request = new DataGridRequest { SearchTerm = "SC-01" }
            }, CancellationToken.None);
        var byCity = await new GetAllPremisesHandler(db, user)
            .Handle(new GetAllPremisesQuery
            {
                Request = new DataGridRequest { SearchTerm = "penang" }
            }, CancellationToken.None);

        Assert.Equal("Main Factory", Assert.Single(byStoreCode.Data).StoreName);
        Assert.Equal("Branch Kitchen", Assert.Single(byCity.Data).StoreName);
    }

    [Fact]
    public async Task Handle_CrossCompanyRows_AreInvisible()
    {
        var userA = PremiseTestData.CompanyUser();
        var userB = PremiseTestData.CompanyUser();
        var databaseName = TestDbFactory.NewDatabaseName();
        var dbA = await PremiseTestData.CreateDbAsync(userA, databaseName);
        await PremiseTestData.SeedPremiseAsync(dbA, userA.CompanyId, name: "Company A premise");
        await PremiseTestData.SeedPremiseAsync(dbA, userB.CompanyId, name: "Company B premise");

        var asCompanyA = await new GetAllPremisesHandler(dbA, userA)
            .Handle(new GetAllPremisesQuery(), CancellationToken.None);
        var asCompanyB = await new GetAllPremisesHandler(
                await PremiseTestData.CreateDbAsync(userB, databaseName), userB)
            .Handle(new GetAllPremisesQuery(), CancellationToken.None);

        Assert.Equal("Company A premise", Assert.Single(asCompanyA.Data).StoreName);
        Assert.Equal("Company B premise", Assert.Single(asCompanyB.Data).StoreName);
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsNotListed()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId, "Doomed premise");
        var premise = await db.Premises.SingleAsync(row => row.Id == premiseId);
        db.Premises.Remove(premise);
        await db.SaveChangesAsync();

        var grid = await new GetAllPremisesHandler(db, user)
            .Handle(new GetAllPremisesQuery(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task Handle_DocumentStatus_IsComputedPerD15ForThePage()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var bareId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId, "Bare premise");
        var completeId = await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, "Complete premise");
        var expiredId = await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, "Expired premise");
        foreach (var documentType in PremiseDocumentStatusCalculator.RequiredDocumentTypes)
        {
            await PremiseTestData.SeedPremiseAttachmentAsync(
                db, user.CompanyId, completeId, documentType);
            // One of the five is expired (D-15 counts the types, not the rows).
            await PremiseTestData.SeedPremiseAttachmentAsync(
                db, user.CompanyId, expiredId, documentType,
                expiryDate: documentType == "HALAL CERTIFICATE"
                    ? new DateTime(2020, 1, 1)
                    : null);
        }

        var grid = await new GetAllPremisesHandler(db, user)
            .Handle(new GetAllPremisesQuery(), CancellationToken.None);

        // D-15 order: no uploads -> NOT COMPLETE; all five -> COMPLETE; one expired -> n OF.
        Assert.Equal(PremiseDocumentStatusCalculator.NotCompleteText,
            grid.Data.Single(row => row.Id == bareId).DocumentStatus);
        Assert.Equal(PremiseDocumentStatusCalculator.CompleteText,
            grid.Data.Single(row => row.Id == completeId).DocumentStatus);
        Assert.Equal("1 OF THE DOCUMENT HAS EXPIRED",
            grid.Data.Single(row => row.Id == expiredId).DocumentStatus);
    }

    [Fact]
    public async Task Handle_Tag_JoinsItsNameAndStaysEmptyWhenUntagged()
    {
        var user = PremiseTestData.CompanyUser();
        var otherCompany = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var tagId = await PremiseTestData.SeedPremiseTagAsync(db, user.CompanyId, "Complete Documentation");
        await PremiseTestData.SeedPremiseTagAsync(db, otherCompany.CompanyId, "Foreign tag");
        var taggedId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId, "Tagged premise");
        var untaggedId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId, "Untagged premise");
        var tagged = await db.Premises.SingleAsync(row => row.Id == taggedId);
        tagged.TagId = tagId;
        await db.SaveChangesAsync();

        var grid = await new GetAllPremisesHandler(db, user)
            .Handle(new GetAllPremisesQuery(), CancellationToken.None);

        var taggedRow = grid.Data.Single(row => row.Id == taggedId);
        Assert.Equal(tagId, taggedRow.TagId);
        Assert.Equal("Complete Documentation", taggedRow.TagName);
        var untaggedRow = grid.Data.Single(row => row.Id == untaggedId);
        Assert.Null(untaggedRow.TagId);
        Assert.Equal(string.Empty, untaggedRow.TagName);
    }

    // PR-04: the seven Halal Information columns are the premise's NEWEST application
    // (spec 7.7 [CONFIRMED] columns; newest-first is our rule), certificate columns
    // following the exactly-one rule (Q17); no batch association -> all null.
    [Fact]
    public async Task Handle_HalalColumns_TakeTheNewestApplicationOfThePremise()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var schemeId = await BatchTestData.FoodPremiseSchemeIdAsync(db);
        var schemeName = await db.Schemes
            .Where(row => row.Id == schemeId)
            .Select(row => row.Name)
            .SingleAsync();
        var appliedId = await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, "Applied premise");
        var bareId = await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, "Bare premise");
        var batchId = await BatchTestData.SeedBatchAsync(
            db, user.CompanyId, schemeId: schemeId, cbReferenceNo: "CB-99");
        await BatchTestData.SeedBatchPremiseAsync(db, user.CompanyId, batchId, appliedId);
        await ApplicationTestData.SeedApplicationAsync(
            db, user.CompanyId,
            referenceNo: "VHS-OLD", schemeId: schemeId, batchId: batchId,
            status: ApplicationStatus.Draft, statusDate: new DateTime(2026, 1, 1));
        var newestApplicationId = await ApplicationTestData.SeedApplicationAsync(
            db, user.CompanyId,
            referenceNo: "VHS-NEW", schemeId: schemeId, batchId: batchId,
            status: ApplicationStatus.ApplicationApproved,
            statusDate: new DateTime(2026, 4, 1));
        await CertificateTestData.SeedCertificateAsync(
            db, user.CompanyId, newestApplicationId,
            certificateNo: "HAL-2026-0099", expiryDate: new DateOnly(2028, 1, 1));

        var grid = await new GetAllPremisesHandler(db, user)
            .Handle(new GetAllPremisesQuery(), CancellationToken.None);

        var applied = grid.Data.Single(row => row.Id == appliedId);
        Assert.Equal("VHS-NEW", applied.VhSmartReferenceNo);
        Assert.Equal("CB-99", applied.CbReferenceNo);
        Assert.Equal(schemeName, applied.Scheme);
        Assert.Equal(ApplicationStatus.ApplicationApproved, applied.ApplicationStatus);
        Assert.Equal("HAL-2026-0099", applied.HalalCertificateNo);
        Assert.Equal(HalalStatus.Valid, applied.HalalCertificateStatus);
        Assert.Equal(new DateOnly(2028, 1, 1), applied.HalalExpiryDate);
        var bare = grid.Data.Single(row => row.Id == bareId);
        Assert.Null(bare.VhSmartReferenceNo);
        Assert.Null(bare.CbReferenceNo);
        Assert.Null(bare.Scheme);
        Assert.Null(bare.ApplicationStatus);
        Assert.Null(bare.HalalCertificateNo);
        Assert.Null(bare.HalalCertificateStatus);
        Assert.Null(bare.HalalExpiryDate);
    }
}

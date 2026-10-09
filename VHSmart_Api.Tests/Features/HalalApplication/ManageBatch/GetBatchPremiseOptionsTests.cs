using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.HalalApplication.ManageBatch.BatchTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;

public class GetBatchPremiseOptionsTests
{
    private static async Task<Guid> FoodPremiseBatchAsync(TestableVHSmartDbContext db) =>
        await SeedBatchAsync(db, CompanyA, schemeId: await FoodPremiseSchemeIdAsync(db));

    [Fact]
    public async Task Handle_Picker_IsOwnCompleteUnlinkedPremises()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await FoodPremiseBatchAsync(db);
        await SeedCompletePremiseAsync(db, CompanyA, name: "Bisa Dipilih");
        var linked = await SeedCompletePremiseAsync(db, CompanyA, name: "Sudah Terpilih");
        await SeedBatchPremiseAsync(db, CompanyA, batchId, linked);
        await SeedIncompletePremiseAsync(db, CompanyA, name: "Belum Lengkap");
        await SeedCompletePremiseAsync(db, CompanyB, name: "Foreign Premise");

        var response = await new GetBatchPremiseOptionsHandler(db, user)
            .Handle(new GetBatchPremiseOptionsQuery(batchId), CancellationToken.None);

        var option = Assert.Single(response.Data);
        Assert.Equal("Bisa Dipilih", option.Name);
        Assert.Equal(1, response.TotalRecords);
    }

    [Fact]
    public async Task Handle_SoftDeletedLink_IsOfferedAgain()
    {
        // AppBatchPremises unlink = soft delete, so the filtered row disappears from the
        // picker and the premise can be associated once more.
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await FoodPremiseBatchAsync(db);
        var premiseId = await SeedCompletePremiseAsync(db, CompanyA, name: "Bisa Dipilih");
        await SeedBatchPremiseAsync(db, CompanyA, batchId, premiseId);
        (await db.BatchPremises.SingleAsync()).IsDeleted = true;
        await db.SaveChangesAsync();

        var response = await new GetBatchPremiseOptionsHandler(db, user)
            .Handle(new GetBatchPremiseOptionsQuery(batchId), CancellationToken.None);

        var option = Assert.Single(response.Data);
        Assert.Equal("Bisa Dipilih", option.Name);
    }

    [Fact]
    public async Task Handle_SearchOnNameOrStoreCode_FiltersRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await FoodPremiseBatchAsync(db);
        await SeedCompletePremiseAsync(
            db, CompanyA, name: "Kilang Santan", storeCode: "SC-100");
        await SeedCompletePremiseAsync(
            db, CompanyA, name: "Kedai Runcit", storeCode: "SC-200");

        var response = await new GetBatchPremiseOptionsHandler(db, user)
            .Handle(
                new GetBatchPremiseOptionsQuery(batchId)
                {
                    Request = { SearchTerm = "santan" }
                },
                CancellationToken.None);

        var byName = Assert.Single(response.Data);
        Assert.Equal("Kilang Santan", byName.Name);

        var byStoreCode = await new GetBatchPremiseOptionsHandler(db, user)
            .Handle(
                new GetBatchPremiseOptionsQuery(batchId)
                {
                    Request = { SearchTerm = "SC-200" }
                },
                CancellationToken.None);

        var option = Assert.Single(byStoreCode.Data);
        Assert.Equal("Kedai Runcit", option.Name);
        Assert.Equal("SC-200", option.StoreCode);
    }

    [Fact]
    public async Task Handle_DefaultOrder_IsNameAscending()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await FoodPremiseBatchAsync(db);
        await SeedCompletePremiseAsync(db, CompanyA, name: "Zulu Premise");
        await SeedCompletePremiseAsync(db, CompanyA, name: "Alpha Premise");

        var response = await new GetBatchPremiseOptionsHandler(db, user)
            .Handle(new GetBatchPremiseOptionsQuery(batchId), CancellationToken.None);

        Assert.Equal(
            new[] { "Alpha Premise", "Zulu Premise" },
            response.Data.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task Handle_NoCompletePremises_ReturnsAnEmptyPage()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await FoodPremiseBatchAsync(db);
        await SeedIncompletePremiseAsync(db, CompanyA, name: "Belum Lengkap");

        var response = await new GetBatchPremiseOptionsHandler(db, user)
            .Handle(new GetBatchPremiseOptionsQuery(batchId), CancellationToken.None);

        Assert.Empty(response.Data);
        Assert.Equal(0, response.TotalRecords);
    }

    [Fact]
    public async Task Handle_UnknownBatch_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetBatchPremiseOptionsHandler(db, user)
                .Handle(
                    new GetBatchPremiseOptionsQuery(Guid.NewGuid()),
                    CancellationToken.None));
    }

    [Fact]
    public async Task Handle_BatchOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignBatch = await SeedBatchAsync(
            db, CompanyB, schemeId: await FoodPremiseSchemeIdAsync(db));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetBatchPremiseOptionsHandler(db, user)
                .Handle(
                    new GetBatchPremiseOptionsQuery(foreignBatch),
                    CancellationToken.None));
    }
}

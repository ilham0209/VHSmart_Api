using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.HalalApplication.ManageBatch.BatchTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;

public class GetAllBatchesTests
{
    private static GetAllBatchesQuery Query(string? searchTerm = null, string? sortBy = null) =>
        new()
        {
            Request = new DataGridRequest
            {
                SearchTerm = searchTerm,
                SortBy = sortBy
            }
        };

    [Fact]
    public async Task Handle_OwnRows_AnswerTheSpecColumns()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await ProductSchemeIdAsync(db);
        var brandId = await SeedBrandAsync(db, name: "SERUNAI");
        var batchId = await SeedBatchAsync(
            db,
            CompanyA,
            name: "Santan Batch Pertama",
            schemeId: schemeId,
            brandId: brandId,
            submissionPlannedDate: new DateTime(2026, 11, 27));

        var response = await new GetAllBatchesHandler(db, user)
            .Handle(Query(), CancellationToken.None);

        var row = Assert.Single(response.Data);
        Assert.Equal(batchId, row.Id);
        Assert.NotEmpty(row.Scheme);
        Assert.Equal("SERUNAI", row.BrandOwner);
        Assert.Equal("SERUNAI", row.Brand);
        Assert.Equal("Santan Batch Pertama", row.BatchName);
        Assert.Equal(string.Empty, row.Premises);
        Assert.Null(row.CbApplicationNo);
        Assert.Equal(new DateTime(2026, 11, 27), row.SubmissionPlannedDate);
        var stored = await db.Batches.SingleAsync();
        Assert.Equal(stored.SysDateCreated, row.CreatedDate);
        Assert.Equal(1, response.TotalRecords);
    }

    [Fact]
    public async Task Handle_FoodPremiseBatch_JoinsItsPremiseNames()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(
            db, CompanyA, schemeId: await FoodPremiseSchemeIdAsync(db));
        var first = await SeedCompletePremiseAsync(db, CompanyA, name: "PREMISE A");
        var second = await SeedCompletePremiseAsync(db, CompanyA, name: "PREMISE B");
        await SeedBatchPremiseAsync(db, CompanyA, batchId, first);
        await SeedBatchPremiseAsync(db, CompanyA, batchId, second);

        var response = await new GetAllBatchesHandler(db, user)
            .Handle(Query(), CancellationToken.None);

        var row = Assert.Single(response.Data);
        Assert.Equal("PREMISE A, PREMISE B", row.Premises);
    }

    [Fact]
    public async Task Handle_SearchOnBatchName_FiltersRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedBatchAsync(db, CompanyA, name: "Santan Batch Pertama");
        await SeedBatchAsync(db, CompanyA, name: "Batch 271125/2");

        var response = await new GetAllBatchesHandler(db, user)
            .Handle(Query(searchTerm: "santan"), CancellationToken.None);

        var row = Assert.Single(response.Data);
        Assert.Equal("Santan Batch Pertama", row.BatchName);
        Assert.Equal(1, response.TotalRecords);
    }

    [Fact]
    public async Task Handle_DefaultOrder_IsNameAscending()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedBatchAsync(db, CompanyA, name: "Zulu Batch");
        await SeedBatchAsync(db, CompanyA, name: "Alpha Batch");

        var response = await new GetAllBatchesHandler(db, user)
            .Handle(Query(), CancellationToken.None);

        Assert.Equal(
            new[] { "Alpha Batch", "Zulu Batch" },
            response.Data.Select(row => row.BatchName).ToArray());
    }

    [Fact]
    public async Task Handle_ClientSort_OverridesTheDefault()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedBatchAsync(db, CompanyA, name: "Alpha Batch");
        await SeedBatchAsync(db, CompanyA, name: "Zulu Batch");

        var query = Query(sortBy: nameof(GetAllBatchesResponse.BatchName));
        query.Request.SortDescending = true;
        var response = await new GetAllBatchesHandler(db, user)
            .Handle(query, CancellationToken.None);

        Assert.Equal(
            new[] { "Zulu Batch", "Alpha Batch" },
            response.Data.Select(row => row.BatchName).ToArray());
    }

    [Fact]
    public async Task Handle_RowsOfOtherCompanies_AreInvisible()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedBatchAsync(db, CompanyB, name: "Foreign Batch");

        var response = await new GetAllBatchesHandler(db, user)
            .Handle(Query(), CancellationToken.None);

        Assert.Empty(response.Data);
        Assert.Equal(0, response.TotalRecords);
    }

    [Fact]
    public async Task Handle_ViewAllToken_StaysOnItsOwnRows()
    {
        // The PR-01 stance: a Switch Company = ALL caller keeps an explicit own-rows filter.
        var viewAll = new TestCurrentUser(
            Guid.NewGuid().ToString(), CompanyA, viewAllCompanies: true);
        var db = await CreateDbAsync(viewAll);
        await SeedBatchAsync(db, CompanyB, name: "Foreign Batch");

        var response = await new GetAllBatchesHandler(db, viewAll)
            .Handle(Query(), CancellationToken.None);

        Assert.Empty(response.Data);
    }
}

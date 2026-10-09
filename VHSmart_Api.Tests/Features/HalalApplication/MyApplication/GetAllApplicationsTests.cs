using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.MyApplication;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

public class GetAllApplicationsTests
{
    [Fact]
    public async Task Handle_ReturnsOnlyTheCallersCompanyRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        await SeedCompanyAsync(db, CompanyB, "Borneo Halal Ventures");
        await SeedApplicationAsync(db, CompanyA, referenceNo: "VHS(PR)/01012026/1");
        await SeedApplicationAsync(db, CompanyB, referenceNo: "VHS(PR)/01012026/2");

        var grid = await new GetAllApplicationsHandler(db, user)
            .Handle(new GetAllApplicationsQuery(), CancellationToken.None);

        var row = Assert.Single(grid.Data);
        Assert.Equal("VHS(PR)/01012026/1", row.ReferenceNo);
        Assert.Equal(1, grid.TotalRecords);

        // The foreign row is still there, behind the tenant filter.
        Assert.Equal(2, await db.Applications.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_ReturnsTheSpec123Columns()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA, "Sereni Trading Sdn Bhd");
        await SeedApplicationAsync(
            db,
            CompanyA,
            referenceNo: "VHS(PR)/01012026/1",
            applicationType: "Renewal",
            status: ApplicationStatus.Draft,
            statusDate: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            cbApplicationNo: "CB-2026-001");

        var grid = await new GetAllApplicationsHandler(db, user)
            .Handle(new GetAllApplicationsQuery(), CancellationToken.None);

        var row = Assert.Single(grid.Data);
        Assert.Equal("Renewal", row.Type);
        Assert.Equal("Sereni Trading Sdn Bhd", row.CompanyName);
        Assert.Equal("VHS(PR)/01012026/1", row.ReferenceNo);
        Assert.Equal("Food and Beverages / Supplement Product", row.Scheme);
        Assert.Null(row.BatchName);
        Assert.Equal("CB-2026-001", row.CbApplicationNo);
        // AppHalalCertificates is HA-06 - the spec column is null for now.
        Assert.Null(row.HalalExpiryDate);
        Assert.Equal(ApplicationStatus.Draft, row.Status);
        Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), row.StatusDate);
    }

    [Fact]
    public async Task Handle_BatchOfTheApplication_FillsTheBatchName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);
        await SeedApplicationAsync(db, CompanyA, batchId: batchId);

        var grid = await new GetAllApplicationsHandler(db, user)
            .Handle(new GetAllApplicationsQuery(), CancellationToken.None);

        var row = Assert.Single(grid.Data);
        Assert.Equal("Santan Batch Pertama", row.BatchName);
        Assert.Equal("Food and Beverages / Supplement Product", row.Scheme);
    }

    [Fact]
    public async Task Handle_DefaultOrder_IsStatusDateDescending()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedApplicationAsync(
            db, CompanyA, referenceNo: "OLDEST",
            statusDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await SeedApplicationAsync(
            db, CompanyA, referenceNo: "NEWEST",
            statusDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        await SeedApplicationAsync(
            db, CompanyA, referenceNo: "MIDDLE",
            statusDate: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

        var grid = await new GetAllApplicationsHandler(db, user)
            .Handle(new GetAllApplicationsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "NEWEST", "MIDDLE", "OLDEST" },
            grid.Data.Select(row => row.ReferenceNo));
    }

    [Fact]
    public async Task Handle_ClientSortByReferenceNoAscending_OverridesTheDefault()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedApplicationAsync(db, CompanyA, referenceNo: "B-2");
        await SeedApplicationAsync(db, CompanyA, referenceNo: "A-1");
        await SeedApplicationAsync(db, CompanyA, referenceNo: "C-3");

        var query = new GetAllApplicationsQuery
        {
            Request = new DataGridRequest
            {
                SortBy = nameof(GetAllApplicationsResponse.ReferenceNo),
                SortDescending = false
            }
        };
        var grid = await new GetAllApplicationsHandler(db, user)
            .Handle(query, CancellationToken.None);

        Assert.Equal(
            new[] { "A-1", "B-2", "C-3" },
            grid.Data.Select(row => row.ReferenceNo));
    }

    [Fact]
    public async Task Handle_PageTwo_ReturnsTheSecondRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedApplicationAsync(db, CompanyA, referenceNo: "FIRST");
        await SeedApplicationAsync(db, CompanyA, referenceNo: "SECOND");

        var query = new GetAllApplicationsQuery
        {
            Request = new DataGridRequest
            {
                Page = 2,
                PageSize = 1,
                // The rows share a StatusDate, so an explicit sort keeps the page order
                // deterministic (default order ties are storage-defined).
                SortBy = nameof(GetAllApplicationsResponse.ReferenceNo),
                SortDescending = false
            }
        };
        var grid = await new GetAllApplicationsHandler(db, user)
            .Handle(query, CancellationToken.None);

        Assert.Equal(2, grid.TotalRecords);
        Assert.Equal("SECOND", Assert.Single(grid.Data).ReferenceNo);
    }

    [Fact]
    public async Task Handle_StatusHistoriesOfAnotherCompany_AreInvisible()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignApplicationId = await SeedApplicationAsync(db, CompanyB);
        db.ApplicationStatusHistories.Add(new ApplicationStatusHistoryEntity
        {
            CompanyId = CompanyB,
            ApplicationId = foreignApplicationId,
            ToStatus = ApplicationStatus.Draft,
            ChangedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        Assert.Equal(0, await db.ApplicationStatusHistories.CountAsync());
        Assert.Equal(1, await db.ApplicationStatusHistories.IgnoreQueryFilters().CountAsync());
    }
}

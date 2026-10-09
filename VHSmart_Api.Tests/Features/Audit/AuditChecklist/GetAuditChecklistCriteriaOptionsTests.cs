using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditChecklist;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.Audit.AuditChecklist.AuditChecklistTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditChecklist;

public class GetAuditChecklistCriteriaOptionsTests
{
    private static GetAuditChecklistCriteriaOptionsHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    private static GetAuditChecklistCriteriaOptionsQuery Query(
        string? searchTerm = null,
        int page = 1,
        int pageSize = 10) =>
        new()
        {
            Request = new DataGridRequest
            {
                SearchTerm = searchTerm,
                Page = page,
                PageSize = pageSize
            }
        };

    [Fact]
    public async Task Handle_ReturnsJoinedCriteriaRowsForTheSelectionTable()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var criteriaId = await SeedCriteriaAsync(
            db, CompanyA,
            categoryName: "PEST CONTROL",
            criteriaText: "Clear quality goals");
        var stored = await db.AuditCriteria.SingleAsync(row => row.Id == criteriaId);
        stored.Reference = "ISO 22000";
        stored.PotentialPoint = 5m;
        await db.SaveChangesAsync();

        var grid = await Handler(db, user).Handle(Query(), CancellationToken.None);

        Assert.Equal(1, grid.TotalRecords);
        var row = Assert.Single(grid.Data);
        Assert.Equal(criteriaId, row.Id);
        Assert.Equal("PEST CONTROL", row.Category);
        Assert.Equal("Clear quality goals", row.Criteria);
        Assert.Equal(string.Empty, row.SubCriteria);
        Assert.Equal("ISO 22000", row.Reference);
        Assert.Equal(5m, row.PotentialPoint);
    }

    [Fact]
    public async Task Handle_DefaultSort_IsCategoryAscending()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCriteriaAsync(db, CompanyA, categoryName: "Zulu section");
        await SeedCriteriaAsync(db, CompanyA, categoryName: "Alfa section");

        var grid = await Handler(db, user).Handle(Query(), CancellationToken.None);

        // The Criteria Selection carries the AU-04 list stance (flagged): no spec order,
        // alphabetical first visible column.
        Assert.Equal(
            new[] { "Alfa section", "Zulu section" },
            grid.Data.Select(row => row.Category));
    }

    [Fact]
    public async Task Handle_Search_CoversTheVisibleColumns()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCriteriaAsync(
            db, CompanyA, categoryName: "Kitchen", criteriaText: "Daily cleaning");
        await SeedCriteriaAsync(
            db, CompanyA, categoryName: "Storage", criteriaText: "Chiller logs kept");

        var byCategory = await Handler(db, user).Handle(
            Query(searchTerm: "kitchen"), CancellationToken.None);
        var byCriteria = await Handler(db, user).Handle(
            Query(searchTerm: "chiller"), CancellationToken.None);

        Assert.Equal("Daily cleaning", Assert.Single(byCategory.Data).Criteria);
        Assert.Equal("Storage", Assert.Single(byCriteria.Data).Category);
    }

    [Fact]
    public async Task Handle_Paging_ReturnsTheRequestedPage()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        for (var i = 1; i <= 12; i++)
            await SeedCriteriaAsync(db, CompanyA, categoryName: $"Category {i:D2}");

        var grid = await Handler(db, user).Handle(
            Query(page: 2, pageSize: 10), CancellationToken.None);

        Assert.Equal(12, grid.TotalRecords);
        Assert.Equal(2, grid.TotalPages);
        Assert.Equal(
            new[] { "Category 11", "Category 12" },
            grid.Data.Select(row => row.Category));
    }

    [Fact]
    public async Task Handle_RowsOfAnotherCompany_AreInvisible()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCriteriaAsync(db, CompanyB);

        var grid = await Handler(db, user).Handle(Query(), CancellationToken.None);

        // The picker only ever offers the caller's own criteria (spec 14.0) - the
        // update validator then accepts exactly this set.
        Assert.Equal(0, grid.TotalRecords);
        Assert.Equal(1, await db.AuditCriteria.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_EmptyCompany_ReturnsEmptyGrid()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var grid = await Handler(db, user).Handle(Query(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
        Assert.Empty(grid.Data);
    }

    [Fact]
    public async Task Handle_IsNotFilteredByChecklistCategory()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        // Criteria whose section heading differs from any checklist category - the open
        // [VERIFY] of spec 14.6 (filter the list by Checklist Category?) proceeds
        // unfiltered (flagged): the task checklist spans numbered categories.
        await SeedCriteriaAsync(db, CompanyA, categoryName: "PEST CONTROL");
        await SeedCriteriaAsync(db, CompanyA, categoryName: "STORAGE");

        var grid = await Handler(db, user).Handle(Query(), CancellationToken.None);

        Assert.Equal(2, grid.TotalRecords);
    }
}

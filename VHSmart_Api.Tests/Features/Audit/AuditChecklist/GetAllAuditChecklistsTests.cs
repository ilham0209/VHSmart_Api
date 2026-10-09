using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditChecklist;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.Audit.AuditChecklist.AuditChecklistTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditChecklist;

public class GetAllAuditChecklistsTests
{
    private static GetAllAuditChecklistsHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    private static GetAllAuditChecklistsQuery Query(
        string? searchTerm = null,
        string? sortBy = null,
        bool descending = false,
        int page = 1,
        int pageSize = 10) =>
        new()
        {
            Request = new DataGridRequest
            {
                SearchTerm = searchTerm,
                SortBy = sortBy,
                SortDescending = descending,
                Page = page,
                PageSize = pageSize
            }
        };

    [Fact]
    public async Task Handle_ReturnsJoinedChecklistCategoryAndOwnColumns()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db, name: "Syariah");
        var rowId = await SeedChecklistAsync(
            db, CompanyA, categoryId: category,
            name: "checklist syariah 2.0", description: "Do not use");

        var grid = await Handler(db, user).Handle(Query(), CancellationToken.None);

        Assert.Equal(1, grid.TotalRecords);
        var row = Assert.Single(grid.Data);
        Assert.Equal(rowId, row.Id);
        Assert.Equal("Syariah", row.ChecklistCategory);
        Assert.Equal("checklist syariah 2.0", row.Name);
        Assert.Equal("Do not use", row.Description);
    }

    [Fact]
    public async Task Handle_DefaultSort_IsChecklistCategoryAscending()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var zulu = await SeedChecklistCategoryAsync(db, name: "Zulu category");
        var alfa = await SeedChecklistCategoryAsync(db, name: "Alfa category");
        await SeedChecklistAsync(db, CompanyA, categoryId: zulu, name: "B checklist");
        await SeedChecklistAsync(db, CompanyA, categoryId: alfa, name: "A checklist");

        var grid = await Handler(db, user).Handle(Query(), CancellationToken.None);

        // The spec states no default order (flagged): alphabetical-first-visible-column.
        Assert.Equal(
            new[] { "Alfa category", "Zulu category" },
            grid.Data.Select(row => row.ChecklistCategory));
    }

    [Fact]
    public async Task Handle_Search_CoversCategoryNameAndDescription()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db, name: "Syariah");
        await SeedChecklistAsync(
            db, CompanyA, categoryId: category,
            name: "Checklist testing", description: "Routine kitchen walk");
        await SeedChecklistAsync(
            db, CompanyA, categoryId: category,
            name: "Second list", description: "Monthly pest checks");

        var byName = await Handler(db, user).Handle(
            Query(searchTerm: "testing"), CancellationToken.None);
        var byCategory = await Handler(db, user).Handle(
            Query(searchTerm: "syariah"), CancellationToken.None);
        var byDescription = await Handler(db, user).Handle(
            Query(searchTerm: "pest"), CancellationToken.None);

        Assert.Equal("Checklist testing", Assert.Single(byName.Data).Name);
        Assert.Equal(2, byCategory.TotalRecords);
        Assert.Equal("Second list", Assert.Single(byDescription.Data).Name);
    }

    [Fact]
    public async Task Handle_Paging_ReturnsTheRequestedPage()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db, name: "Same category");
        for (var i = 1; i <= 12; i++)
            await SeedChecklistAsync(db, CompanyA, categoryId: category, name: $"Checklist {i:D2}");

        var grid = await Handler(db, user).Handle(
            Query(page: 2, pageSize: 10), CancellationToken.None);

        // Category ties break into Name order from the underlying rows; assert paging
        // volume only (the spec states no default order at all).
        Assert.Equal(12, grid.TotalRecords);
        Assert.Equal(2, grid.TotalPages);
        Assert.Equal(2, grid.Data.Count());
    }

    [Fact]
    public async Task Handle_RowsOfAnotherCompany_AreInvisible()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedChecklistAsync(db, CompanyB);

        var grid = await Handler(db, user).Handle(Query(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
        Assert.Equal(1, await db.AuditChecklists.IgnoreQueryFilters().CountAsync());
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
    public async Task Handle_SortByNameDescending()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db, name: "Same category");
        await SeedChecklistAsync(db, CompanyA, categoryId: category, name: "Alpha");
        await SeedChecklistAsync(db, CompanyA, categoryId: category, name: "Zulu");

        var grid = await Handler(db, user).Handle(
            Query(sortBy: nameof(GetAllAuditChecklistsResponse.Name), descending: true),
            CancellationToken.None);

        Assert.Equal("Zulu", grid.Data.First().Name);
        Assert.Equal("Alpha", grid.Data.Last().Name);
    }
}

using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditCriteria;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.Audit.AuditCriteria.AuditCriteriaTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditCriteria;

public class GetAllAuditCriteriaTests
{
    private static GetAllAuditCriteriaHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    private static GetAllAuditCriteriaQuery Query(
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
    public async Task Handle_ReturnsJoinedCategoryCriteriaAndSubCriteriaTexts()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db, name: "Storage");
        var criteria = await SeedMasterAsync(db, CompanyA, text: "Temperature control");
        var subCriteria = await SeedMasterAsync(
            db, CompanyA, AuditCriteriaMasterKind.SubCriteria, "Chiller logs");
        var rowId = await SeedCriteriaAsync(
            db, CompanyA,
            categoryId: category,
            criteriaId: criteria,
            subCriteriaId: subCriteria,
            reference: "ISO 22000 Clause 8",
            potentialPoint: 5m,
            description: "Daily logs kept");

        var grid = await Handler(db, user).Handle(Query(), CancellationToken.None);

        Assert.Equal(1, grid.TotalRecords);
        var row = Assert.Single(grid.Data);
        Assert.Equal(rowId, row.Id);
        Assert.Equal("Storage", row.Category);
        Assert.Equal("Temperature control", row.Criteria);
        Assert.Equal("Chiller logs", row.SubCriteria);
        Assert.Equal("ISO 22000 Clause 8", row.Reference);
        Assert.Equal(5m, row.PotentialPoint);
        Assert.Equal("Daily logs kept", row.Description);
    }

    [Fact]
    public async Task Handle_NoSubCriteria_ReturnsEmptyText()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCriteriaAsync(db, CompanyA);

        var grid = await Handler(db, user).Handle(Query(), CancellationToken.None);

        var row = Assert.Single(grid.Data);
        Assert.Equal(string.Empty, row.SubCriteria);
    }

    [Fact]
    public async Task Handle_DefaultSort_IsCategoryAscending()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var zulu = await SeedCategoryAsync(db, name: "Zulu category");
        var alfa = await SeedCategoryAsync(db, name: "Alfa category");
        await SeedCriteriaAsync(db, CompanyA, categoryId: zulu, criteriaId: criteria);
        await SeedCriteriaAsync(db, CompanyA, categoryId: alfa, criteriaId: criteria);

        var grid = await Handler(db, user).Handle(Query(), CancellationToken.None);

        // The spec states no default order (flagged): the alphabetical-first-visible-column
        // stance of AU-02/AU-03.
        Assert.Equal(
            new[] { "Alfa category", "Zulu category" },
            grid.Data.Select(row => row.Category));
    }

    [Fact]
    public async Task Handle_Search_CoversCategoryCriteriaSubCriteriaReferenceAndDescription()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var criteria = await SeedMasterAsync(db, CompanyA, text: "Cleanliness");
        await SeedCriteriaAsync(
            db, CompanyA, criteriaId: criteria,
            reference: "ISO 9001", description: "Kitchen daily checklist");
        var otherCriteria = await SeedMasterAsync(db, CompanyA, text: "Pest control");
        await SeedCriteriaAsync(
            db, CompanyA, criteriaId: otherCriteria,
            description: "Monthly pest reports");

        var byCategory = await Handler(db, user).Handle(
            Query(searchTerm: "Kitchen"), CancellationToken.None);
        var byCriteria = await Handler(db, user).Handle(
            Query(searchTerm: "pest"), CancellationToken.None);
        var byReference = await Handler(db, user).Handle(
            Query(searchTerm: "ISO 9001"), CancellationToken.None);
        var byDescription = await Handler(db, user).Handle(
            Query(searchTerm: "monthly"), CancellationToken.None);

        Assert.Equal(2, byCategory.TotalRecords);
        Assert.Equal("Pest control", Assert.Single(byCriteria.Data).Criteria);
        Assert.Single(byReference.Data);
        Assert.Equal("Monthly pest reports", Assert.Single(byDescription.Data).Description);
    }

    [Fact]
    public async Task Handle_Paging_ReturnsTheRequestedPage()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var criteria = await SeedMasterAsync(db, CompanyA);
        for (var i = 1; i <= 12; i++)
        {
            var category = await SeedCategoryAsync(db, name: $"Category {i:D2}");
            await SeedCriteriaAsync(db, CompanyA, categoryId: category, criteriaId: criteria);
        }

        var grid = await Handler(db, user).Handle(
            Query(page: 2, pageSize: 10), CancellationToken.None);

        // Category ascending: page 2 holds the last two rows.
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
    public async Task Handle_SortByReferenceDescending()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var categoryA = await SeedCategoryAsync(db, name: "Category A");
        var categoryB = await SeedCategoryAsync(db, name: "Category B");
        await SeedCriteriaAsync(
            db, CompanyA, categoryId: categoryA, criteriaId: criteria, reference: "AAA");
        await SeedCriteriaAsync(
            db, CompanyA, categoryId: categoryB, criteriaId: criteria, reference: "ZZZ");

        var grid = await Handler(db, user).Handle(
            Query(sortBy: nameof(GetAllAuditCriteriaResponse.Reference), descending: true),
            CancellationToken.None);

        Assert.Equal(2, grid.TotalRecords);
        var first = grid.Data.First();
        Assert.Equal("ZZZ", first.Reference);
        Assert.Equal("Category B", first.Category);
    }
}

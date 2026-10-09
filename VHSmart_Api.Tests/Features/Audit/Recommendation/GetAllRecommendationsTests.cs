using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.Recommendation;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.Audit.Recommendation.RecommendationTestData;

namespace VHSmart_Api.Tests.Features.Audit.Recommendation;

public class GetAllRecommendationsTests
{
    private static GetAllRecommendationsHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_NoClientSort_DefaultsToNameAscending()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedRecommendationAsync(db, CompanyA, name: "Zebra rule", recommendationCode: "R9");
        await SeedRecommendationAsync(db, CompanyA, name: "Alpha rule", recommendationCode: "R1");

        var grid = await Handler(db, user).Handle(
            new GetAllRecommendationsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "Alpha rule", "Zebra rule" },
            grid.Data.Select(row => row.Name));
    }

    [Fact]
    public async Task Handle_SortByModifiedDateDescending_PutsEditedRowsFirst()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var editedId = await SeedRecommendationAsync(db, CompanyA, name: "Edited rule");
        var untouchedId = await SeedRecommendationAsync(db, CompanyA, name: "Untouched rule");

        // Editing stamps SysDateModified (the "Modified Date" column of spec 14.3).
        await new UpdateRecommendationHandler(db, user).Handle(
            new UpdateRecommendationCommand(editedId, "Edited rule", "R1", "touched"),
            CancellationToken.None);

        var grid = await Handler(db, user).Handle(
            new GetAllRecommendationsQuery
            {
                Request = new DataGridRequest
                {
                    SortBy = "ModifiedDate",
                    SortDescending = true
                }
            },
            CancellationToken.None);

        var rows = grid.Data.ToList();
        Assert.Equal(editedId, rows[0].Id);
        Assert.NotNull(rows[0].ModifiedDate);
        Assert.Null(rows[1].ModifiedDate);
        Assert.NotEqual(untouchedId, rows[0].Id);
    }

    [Fact]
    public async Task Handle_Search_CoversNameCodeAndDescription()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedRecommendationAsync(
            db, CompanyA, name: "Santan Berkualiti", recommendationCode: "R111",
            description: "Kelapa berkualiti tinggi");
        await SeedRecommendationAsync(
            db, CompanyA, name: "Kebersihan premis", recommendationCode: "r3",
            description: "Bersih dan selamat");

        var byName = await Handler(db, user).Handle(
            new GetAllRecommendationsQuery
            { Request = new DataGridRequest { SearchTerm = "berkualiti" } },
            CancellationToken.None);
        var byCode = await Handler(db, user).Handle(
            new GetAllRecommendationsQuery
            { Request = new DataGridRequest { SearchTerm = "R111" } },
            CancellationToken.None);
        var byDescription = await Handler(db, user).Handle(
            new GetAllRecommendationsQuery
            { Request = new DataGridRequest { SearchTerm = "selamat" } },
            CancellationToken.None);

        Assert.Equal("Santan Berkualiti", Assert.Single(byName.Data).Name);
        Assert.Equal("R111", Assert.Single(byCode.Data).RecommendationCode);
        Assert.Equal("Kebersihan premis", Assert.Single(byDescription.Data).Name);
    }

    [Fact]
    public async Task Handle_RowsOfAnotherCompany_AreInvisible()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedRecommendationAsync(db, CompanyB, name: "Foreign rule");

        var grid = await Handler(db, user).Handle(
            new GetAllRecommendationsQuery(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
        // The foreign row is still there - only the tenant scope hides it.
        Assert.Equal(1, await db.Recommendations.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_EmptyCompany_ReturnsEmptyGrid()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var grid = await Handler(db, user).Handle(
            new GetAllRecommendationsQuery(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
        Assert.Empty(grid.Data);
    }
}

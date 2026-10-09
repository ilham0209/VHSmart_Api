using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.Finding;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.Audit.Finding.FindingTestData;

namespace VHSmart_Api.Tests.Features.Audit.Finding;

public class GetFindingRecommendationOptionsTests
{
    private static GetFindingRecommendationOptionsHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ReturnsOwnCompanyRowsSortedByNameAscending()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedRecommendationAsync(db, CompanyA, name: "Zulu rule", recommendationCode: "R9");
        await SeedRecommendationAsync(db, CompanyA, name: "Alfa rule", recommendationCode: "R1");
        await SeedRecommendationAsync(db, CompanyB, name: "Foreign rule");

        var grid = await Handler(db, user).Handle(
            new GetFindingRecommendationOptionsQuery(), CancellationToken.None);

        // The picker only ever offers the caller's own company rows (spec 14.0) - the
        // validator then accepts exactly this set.
        Assert.Equal(
            new[] { "Alfa rule", "Zulu rule" },
            grid.Data.Select(row => row.Name));
        Assert.Equal(2, grid.TotalRecords);
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
            new GetFindingRecommendationOptionsQuery
            { Request = new DataGridRequest { SearchTerm = "berkualiti" } },
            CancellationToken.None);
        var byCode = await Handler(db, user).Handle(
            new GetFindingRecommendationOptionsQuery
            { Request = new DataGridRequest { SearchTerm = "R111" } },
            CancellationToken.None);
        var byDescription = await Handler(db, user).Handle(
            new GetFindingRecommendationOptionsQuery
            { Request = new DataGridRequest { SearchTerm = "selamat" } },
            CancellationToken.None);

        Assert.Equal("Santan Berkualiti", Assert.Single(byName.Data).Name);
        Assert.Equal("R111", Assert.Single(byCode.Data).RecommendationCode);
        Assert.Equal("Kebersihan premis", Assert.Single(byDescription.Data).Name);
    }

    [Fact]
    public async Task Handle_Paging_ReturnsTheRequestedPage()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        for (var i = 1; i <= 12; i++)
            await SeedRecommendationAsync(
                db, CompanyA, name: $"Rule {i:D2}", recommendationCode: $"R{i:D2}");

        var grid = await Handler(db, user).Handle(
            new GetFindingRecommendationOptionsQuery
            { Request = new DataGridRequest { Page = 2, PageSize = 10 } },
            CancellationToken.None);

        // Name ascending: page 2 holds the last two rows (the spec's checkbox table pages
        // through the same grid helpers as every other list).
        Assert.Equal(12, grid.TotalRecords);
        Assert.Equal(2, grid.TotalPages);
        Assert.Equal(
            new[] { "Rule 11", "Rule 12" },
            grid.Data.Select(row => row.Name));
    }

    [Fact]
    public async Task Handle_RowsOfAnotherCompany_AreInvisible()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedRecommendationAsync(db, CompanyB, name: "Foreign rule");

        var grid = await Handler(db, user).Handle(
            new GetFindingRecommendationOptionsQuery(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
        Assert.Equal(1, await db.Recommendations.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_EmptyCompany_ReturnsEmptyGrid()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var grid = await Handler(db, user).Handle(
            new GetFindingRecommendationOptionsQuery(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
        Assert.Empty(grid.Data);
    }
}

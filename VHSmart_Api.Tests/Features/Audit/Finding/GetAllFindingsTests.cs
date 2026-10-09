using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.Finding;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.Audit.Finding.FindingTestData;

namespace VHSmart_Api.Tests.Features.Audit.Finding;

public class GetAllFindingsTests
{
    private static GetAllFindingsHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_NoClientSort_DefaultsToNameAscending()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedFindingAsync(db, CompanyA, name: "Zebra statement", findingCode: "F9");
        await SeedFindingAsync(db, CompanyA, name: "Alpha statement", findingCode: "F1");

        var grid = await Handler(db, user).Handle(
            new GetAllFindingsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "Alpha statement", "Zebra statement" },
            grid.Data.Select(row => row.Name));
    }

    [Fact]
    public async Task Handle_SortByModifiedDateDescending_PutsEditedRowsFirst()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var reco = await SeedRecommendationAsync(db, CompanyA);
        var editedId = await SeedFindingAsync(db, CompanyA, name: "Edited statement");
        var untouchedId = await SeedFindingAsync(db, CompanyA, name: "Untouched statement");

        // Editing stamps SysDateModified (the "Modified Date" column of spec 14.4).
        await new UpdateFindingHandler(db, user).Handle(
            new UpdateFindingCommand(editedId, "Edited statement", "F1", null, [reco]),
            CancellationToken.None);

        var grid = await Handler(db, user).Handle(
            new GetAllFindingsQuery
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
    public async Task Handle_RecommendationSelection_JoinsTheLinkedNamesAlphabetically()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var zulu = await SeedRecommendationAsync(db, CompanyA, name: "Zulu rule");
        var alfa = await SeedRecommendationAsync(db, CompanyA, name: "Alfa rule");
        var findingId = await SeedFindingAsync(db, CompanyA);
        await SeedFindingLinkAsync(db, CompanyA, findingId, zulu);
        await SeedFindingLinkAsync(db, CompanyA, findingId, alfa);

        var grid = await Handler(db, user).Handle(
            new GetAllFindingsQuery(), CancellationToken.None);

        // The cell is filled AFTER paging (a collection never decides the row order) and
        // the names join alphabetically so the cell text is deterministic.
        Assert.Equal("Alfa rule, Zulu rule", Assert.Single(grid.Data).RecommendationSelection);
    }

    [Fact]
    public async Task Handle_Search_CoversNameAndFindingCode()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedFindingAsync(
            db, CompanyA,
            name: "Portion sizes are consistent with the menu descriptions.",
            findingCode: "Portion sizes");
        await SeedFindingAsync(
            db, CompanyA, name: "Staff wear clean uniforms", findingCode: "Uniforms");

        var byName = await Handler(db, user).Handle(
            new GetAllFindingsQuery
            { Request = new DataGridRequest { SearchTerm = "uniforms" } },
            CancellationToken.None);
        var byCode = await Handler(db, user).Handle(
            new GetAllFindingsQuery
            { Request = new DataGridRequest { SearchTerm = "Portion" } },
            CancellationToken.None);

        Assert.Equal("Staff wear clean uniforms", Assert.Single(byName.Data).Name);
        Assert.Equal(
            "Portion sizes", Assert.Single(byCode.Data).FindingCode);
    }

    [Fact]
    public async Task Handle_RowsOfAnotherCompany_AreInvisible()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedFindingAsync(db, CompanyB, name: "Foreign statement");

        var grid = await Handler(db, user).Handle(
            new GetAllFindingsQuery(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
        // The foreign row is still there - only the tenant scope hides it.
        Assert.Equal(1, await db.Findings.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_EmptyCompany_ReturnsEmptyGrid()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var grid = await Handler(db, user).Handle(
            new GetAllFindingsQuery(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
        Assert.Empty(grid.Data);
    }
}

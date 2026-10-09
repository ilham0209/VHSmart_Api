using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditPrefix;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.Audit.AuditPrefix.AuditPrefixTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditPrefix;

public class GetAllAuditPrefixesTests
{
    private static GetAllAuditPrefixesHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_DefaultSort_NewestModifiedFirst()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var editedBrand = await SeedBrandAsync(db, name: "NATURAL");
        var editedId = await SeedPrefixAsync(db, CompanyA, brandId: editedBrand, prefix: "MRS");
        var untouchedId = await SeedPrefixAsync(db, CompanyA, prefix: "SRN");

        // Editing stamps SysDateModified (the "Modified Date" column of spec 14.2).
        await new UpdateAuditPrefixHandler(db, user).Handle(
            new UpdateAuditPrefixCommand(editedId, editedBrand, "MRS", "touched"),
            CancellationToken.None);

        var grid = await Handler(db, user).Handle(
            new GetAllAuditPrefixesQuery(), CancellationToken.None);

        Assert.Equal(2, grid.TotalRecords);
        var rows = grid.Data.ToList();
        Assert.Equal(editedId, rows[0].Id);
        Assert.NotNull(rows[0].ModifiedDate);
        Assert.Null(rows[1].ModifiedDate);
        Assert.NotEqual(untouchedId, rows[0].Id);
    }

    [Fact]
    public async Task Handle_JoinsTheBrandNameAndDescription()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedPrefixAsync(
            db, CompanyA, prefix: "MRS", description: "Natural brand");
        await SeedPrefixAsync(
            db, CompanyA, prefix: "RTW", brandId: await SeedBrandAsync(db, name: "RTW"));

        var grid = await Handler(db, user).Handle(
            new GetAllAuditPrefixesQuery(), CancellationToken.None);

        Assert.Equal(2, grid.TotalRecords);
        Assert.Contains(grid.Data, row =>
            row.AuditPrefix == "MRS"
            && row.Brand == "NATURAL"
            && row.Description == "Natural brand");
        Assert.Contains(grid.Data, row => row.AuditPrefix == "RTW" && row.Brand == "RTW");
    }

    [Fact]
    public async Task Handle_Search_CoversPrefixAndDescription()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedPrefixAsync(db, CompanyA, prefix: "MRS", description: "Natural brand");
        await SeedPrefixAsync(
            db, CompanyA, prefix: "SRN", brandId: await SeedBrandAsync(db, name: "SERUNAI"),
            description: "Serunai brand");

        var byPrefix = await Handler(db, user).Handle(
            new GetAllAuditPrefixesQuery
            {
                Request = new DataGridRequest { SearchTerm = "srn" }
            },
            CancellationToken.None);
        var byDescription = await Handler(db, user).Handle(
            new GetAllAuditPrefixesQuery
            {
                Request = new DataGridRequest { SearchTerm = "natural" }
            },
            CancellationToken.None);

        Assert.Equal(1, byPrefix.TotalRecords);
        Assert.Equal("SRN", Assert.Single(byPrefix.Data).AuditPrefix);
        Assert.Equal(1, byDescription.TotalRecords);
        Assert.Equal("MRS", Assert.Single(byDescription.Data).AuditPrefix);
    }

    [Fact]
    public async Task Handle_RowsOfAnotherCompany_AreInvisible()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedPrefixAsync(db, CompanyB, prefix: "FOREIGN");

        var grid = await Handler(db, user).Handle(
            new GetAllAuditPrefixesQuery(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
        // The foreign row is still there - only the tenant scope hides it.
        Assert.Equal(1, await db.AuditPrefixes.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_SortByAuditPrefixAscending_OrdersAlphabetically()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedPrefixAsync(db, CompanyA, prefix: "RTW");
        await SeedPrefixAsync(
            db, CompanyA, prefix: "MRS", brandId: await SeedBrandAsync(db, name: "SERUNAI"));
        await SeedPrefixAsync(
            db, CompanyA, prefix: "AAA", brandId: await SeedBrandAsync(db, name: "ZENITH"));

        var grid = await Handler(db, user).Handle(
            new GetAllAuditPrefixesQuery
            {
                Request = new DataGridRequest { SortBy = "AuditPrefix", SortDescending = false }
            },
            CancellationToken.None);

        Assert.Equal(
            new[] { "AAA", "MRS", "RTW" },
            grid.Data.Select(row => row.AuditPrefix));
    }

    [Fact]
    public async Task Handle_EmptyCompany_ReturnsEmptyGrid()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var grid = await Handler(db, user).Handle(
            new GetAllAuditPrefixesQuery(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
        Assert.Empty(grid.Data);
    }
}

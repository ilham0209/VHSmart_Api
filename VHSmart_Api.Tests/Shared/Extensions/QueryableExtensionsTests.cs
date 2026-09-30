using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Shared.Extensions;

public class QueryableExtensionsTests
{
    [Fact]
    public async Task ApplySearch_MatchingTerm_IsCaseInsensitiveSubstringMatch()
    {
        var dbName = await SeedAsync("Apple pie", "Banana bread", "Cherry jam");
        var db = TestDbFactory.Create(dbName);

        var result = await db.TestGlobalRecords.ApplySearch("PIE", "Name").ToListAsync();

        var match = Assert.Single(result);
        Assert.Equal("Apple pie", match.Name);
    }

    [Fact]
    public void ApplySearch_MissingOrBlankTerm_ReturnsQueryUnchanged()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        var query = db.TestGlobalRecords;

        Assert.Same(query, query.ApplySearch(null, "Name"));
        Assert.Same(query, query.ApplySearch("   ", "Name"));
        Assert.Same(query, query.ApplySearch("term"));
    }

    [Fact]
    public async Task ApplySearch_NullColumnValue_DoesNotThrow()
    {
        var dbName = TestDbFactory.NewDatabaseName();
        await using var seedDb = TestDbFactory.Create(dbName);
        seedDb.TestGlobalRecords.Add(new TestGlobalRecord { Name = "Apple", Note = "organic" });
        seedDb.TestGlobalRecords.Add(new TestGlobalRecord { Name = "Apple pie" });
        await seedDb.SaveChangesAsync();
        var db = TestDbFactory.Create(dbName);

        var result = await db.TestGlobalRecords.ApplySearch("ORGANIC", "Note").ToListAsync();

        Assert.Equal("Apple", Assert.Single(result).Name);
    }

    [Fact]
    public void ApplySearch_UnknownProperty_ThrowsArgumentException()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());

        var exception = Assert.Throws<ArgumentException>(() => db.TestGlobalRecords.ApplySearch("term", "Missing"));

        Assert.Contains("Missing", exception.Message);
    }

    [Fact]
    public void ApplySearch_NonStringProperty_ThrowsArgumentException()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());

        Assert.Throws<ArgumentException>(() => db.TestGlobalRecords.ApplySearch("term", "Id"));
    }

    [Fact]
    public async Task ApplySort_Ascending_OrdersRows()
    {
        var dbName = await SeedAsync("Banana", "Apple", "Cherry");
        var db = TestDbFactory.Create(dbName);

        var result = await db.TestGlobalRecords.ApplySort("Name", sortDescending: false).ToListAsync();

        Assert.Equal(new[] { "Apple", "Banana", "Cherry" }, result.Select(x => x.Name).ToArray());
    }

    [Fact]
    public async Task ApplySort_Descending_OrdersRows()
    {
        var dbName = await SeedAsync("Banana", "Apple", "Cherry");
        var db = TestDbFactory.Create(dbName);

        var result = await db.TestGlobalRecords.ApplySort("Name", sortDescending: true).ToListAsync();

        Assert.Equal(new[] { "Cherry", "Banana", "Apple" }, result.Select(x => x.Name).ToArray());
    }

    [Fact]
    public void ApplySort_NoSortBy_ReturnsQueryUnchanged()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        var query = db.TestGlobalRecords;

        Assert.Same(query, query.ApplySort(null, false));
        Assert.Same(query, query.ApplySort("  ", true));
    }

    [Fact]
    public void ApplySort_UnknownProperty_ThrowsArgumentException()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());

        var exception = Assert.Throws<ArgumentException>(() => db.TestGlobalRecords.ApplySort("Missing", false));

        Assert.Contains("Missing", exception.Message);
    }

    [Fact]
    public async Task ToDataGridResponse_RequestedPage_ReturnsPageAndTotals()
    {
        var dbName = await SeedNumberedAsync(25);
        var db = TestDbFactory.Create(dbName);

        var result = await db.TestGlobalRecords.ToDataGridResponseAsync(new DataGridRequest { Page = 2, PageSize = 10 });

        Assert.Equal(10, result.Data.Count());
        Assert.Equal(25, result.TotalRecords);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.CurrentPage);
        Assert.Equal(10, result.PageSize);
        Assert.True(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public async Task ToDataGridResponse_LastPage_ReturnsRemainder()
    {
        var dbName = await SeedNumberedAsync(25);
        var db = TestDbFactory.Create(dbName);

        var result = await db.TestGlobalRecords.ToDataGridResponseAsync(new DataGridRequest { Page = 3, PageSize = 10 });

        Assert.Equal(5, result.Data.Count());
        Assert.False(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public async Task ToDataGridResponse_PageBeyondRange_ReturnsEmptyPage()
    {
        var dbName = await SeedNumberedAsync(25);
        var db = TestDbFactory.Create(dbName);

        var result = await db.TestGlobalRecords.ToDataGridResponseAsync(new DataGridRequest { Page = 99, PageSize = 10 });

        Assert.Empty(result.Data);
        Assert.Equal(25, result.TotalRecords);
        Assert.False(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public async Task ToDataGridResponse_ExcessivePageSize_ClampsToMaxPageSize()
    {
        var dbName = await SeedNumberedAsync(3);
        var db = TestDbFactory.Create(dbName);

        var result = await db.TestGlobalRecords.ToDataGridResponseAsync(new DataGridRequest { PageSize = 10_000 });

        Assert.Equal(DataGridRequest.MaxPageSize, result.PageSize);
        Assert.Equal(3, result.TotalRecords);
    }

    [Fact]
    public async Task ToDataGridResponse_PageAndSizeBelowOne_UsesDefaults()
    {
        var dbName = await SeedNumberedAsync(3);
        var db = TestDbFactory.Create(dbName);

        var result = await db.TestGlobalRecords.ToDataGridResponseAsync(new DataGridRequest { Page = 0, PageSize = -5 });

        Assert.Equal(1, result.CurrentPage);
        Assert.Equal(DataGridRequest.DefaultPageSize, result.PageSize);
    }

    [Fact]
    public async Task ToDataGridResponse_SortedDuplicateValues_PagesAreDisjoint()
    {
        var dbName = await SeedAsync(Enumerable.Repeat("Same", 15).ToArray());
        var seenIds = new HashSet<Guid>();

        for (var page = 1; page <= 3; page++)
        {
            var db = TestDbFactory.Create(dbName);
            var result = await db.TestGlobalRecords
                .ApplySort("Name", false)
                .ToDataGridResponseAsync(new DataGridRequest { Page = page, PageSize = 5 });

            foreach (var record in result.Data)
                Assert.True(seenIds.Add(record.Id), $"Row {record.Id} was returned on two pages.");
        }

        Assert.Equal(15, seenIds.Count);
    }

    [Fact]
    public async Task ToDataGridResponse_WithSearchSortAndPage_AppliesAll()
    {
        var dbName = await SeedNumberedAsync(25);
        var db = TestDbFactory.Create(dbName);

        var result = await db.TestGlobalRecords
            .ApplySearch("record 1", "Name")
            .ApplySort("Name", sortDescending: true)
            .ToDataGridResponseAsync(new DataGridRequest { Page = 1, PageSize = 4 });

        Assert.Equal(10, result.TotalRecords);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(4, result.Data.Count());
        Assert.Equal("Record 19", result.Data.First().Name);
    }

    private static async Task<string> SeedAsync(params string[] names)
    {
        var dbName = TestDbFactory.NewDatabaseName();
        await using var db = TestDbFactory.Create(dbName);

        foreach (var name in names)
            db.TestGlobalRecords.Add(new TestGlobalRecord { Name = name });

        await db.SaveChangesAsync();
        return dbName;
    }

    private static async Task<string> SeedNumberedAsync(int count)
    {
        var dbName = TestDbFactory.NewDatabaseName();
        await using var db = TestDbFactory.Create(dbName);

        for (var i = 1; i <= count; i++)
            db.TestGlobalRecords.Add(new TestGlobalRecord { Name = $"Record {i:D2}" });

        await db.SaveChangesAsync();
        return dbName;
    }
}

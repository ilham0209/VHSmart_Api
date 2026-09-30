using VHSmart_Api.Features.Admin.WebLinks;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Admin.WebLinks;

public class GetAllWebLinksTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    private static TestCurrentUser UserB() => new(Guid.NewGuid().ToString(), CompanyB);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static WebLinkEntity NewRow(Guid companyId, string name, string webpage, string? description = null) =>
        new()
        {
            CompanyId = companyId,
            Name = name,
            Webpage = webpage,
            Description = description,
            Icon = new() { FileName = "icon.png", StorageKey = Guid.NewGuid().ToString("D"), ContentType = "image/png" }
        };

    [Fact]
    public async Task Handle_SearchTerm_FindsNameWebpageOrDescription()
    {
        var db = await CreateDbAsync(UserA());
        db.WebLinks.AddRange(
            NewRow(CompanyA, "Santan", "https://santan.example"),
            NewRow(CompanyA, "Verify Halal", "https://verifyhalal.com"),
            NewRow(CompanyA, "Serunai", "https://serunai.example", "halal assurance platform"));
        await db.SaveChangesAsync();

        var byName = await QueryAsync(db, "Santan");
        var byWebpage = await QueryAsync(db, "verifyhalal");
        var byDescription = await QueryAsync(db, "assurance");

        Assert.Equal("Santan", Assert.Single(byName.Data).Name);
        Assert.Equal("Verify Halal", Assert.Single(byWebpage.Data).Name);
        Assert.Equal("Serunai", Assert.Single(byDescription.Data).Name);
    }

    [Fact]
    public async Task Handle_CrossCompanyRows_AreInvisible()
    {
        var userA = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        db.WebLinks.AddRange(
            NewRow(CompanyA, "Company A link", "https://a.example"),
            NewRow(CompanyB, "Company B link", "https://b.example"));
        await db.SaveChangesAsync();

        var asCompanyA = await new GetAllWebLinksHandler(db)
            .Handle(new GetAllWebLinksQuery(), CancellationToken.None);
        var asCompanyB = await new GetAllWebLinksHandler(
                TestDbFactory.Create(databaseName, UserB()))
            .Handle(new GetAllWebLinksQuery(), CancellationToken.None);

        Assert.Equal("Company A link", Assert.Single(asCompanyA.Data).Name);
        Assert.Equal("Company B link", Assert.Single(asCompanyB.Data).Name);
    }

    [Fact]
    public async Task Handle_SortBySysDateModified_NewestFirst()
    {
        var db = await CreateDbAsync(UserA());
        var older = NewRow(CompanyA, "Older link", "https://old.example");
        older.SysDateModified = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = NewRow(CompanyA, "Newer link", "https://new.example");
        newer.SysDateModified = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        db.WebLinks.AddRange(older, newer);
        await db.SaveChangesAsync();

        var result = await new GetAllWebLinksHandler(db)
            .Handle(
                new GetAllWebLinksQuery
                {
                    Request = new DataGridRequest
                    {
                        SortBy = "SysDateModified",
                        SortDescending = true
                    }
                },
                CancellationToken.None);

        Assert.Equal(["Newer link", "Older link"], result.Data.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsNotListed()
    {
        var db = await CreateDbAsync(UserA());
        var row = NewRow(CompanyA, "Doomed link", "https://doomed.example");
        db.WebLinks.Add(row);
        await db.SaveChangesAsync();
        db.WebLinks.Remove(row);
        await db.SaveChangesAsync();

        var result = await new GetAllWebLinksHandler(db)
            .Handle(new GetAllWebLinksQuery(), CancellationToken.None);

        Assert.Equal(0, result.TotalRecords);
    }

    private static Task<DataGridResponse<GetAllWebLinksResponse>> QueryAsync(
        TestableVHSmartDbContext db,
        string searchTerm) =>
        new GetAllWebLinksHandler(db).Handle(
            new GetAllWebLinksQuery { Request = new DataGridRequest { SearchTerm = searchTerm } },
            CancellationToken.None);
}

using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.WebLinks;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.WebLinks;

public class DeleteWebLinkTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static WebLinkEntity NewRow(Guid companyId, string name) =>
        new()
        {
            CompanyId = companyId,
            Name = name,
            Webpage = "https://verifyhalal.com",
            Icon = new()
            {
                FileName = "icon.png",
                StorageKey = Guid.NewGuid().ToString("D"),
                ContentType = "image/png"
            }
        };

    [Fact]
    public async Task Handle_ExistingRow_SoftDeletesAndClearsTheName()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        var row = NewRow(CompanyA, "Verify Halal");
        db.WebLinks.Add(row);
        await db.SaveChangesAsync();

        await new DeleteWebLinkHandler(db)
            .Handle(new DeleteWebLinkCommand(row.Id), CancellationToken.None);

        // CodingRules 7.1: the row stays, the flag flips; the global filter hides it.
        var stored = await db.WebLinks.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        Assert.True(stored.IsDeleted);
        Assert.Empty(await db.WebLinks.ToArrayAsync());

        // The name is free again for a new live row.
        Assert.False(await db.WebLinks.AnyAsync(link => link.Name == "Verify Halal"));
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteWebLinkHandler(db)
                .Handle(new DeleteWebLinkCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CrossCompanyRow_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        await db.Database.EnsureCreatedAsync();
        var foreignRow = NewRow(CompanyB, "Company B link");
        db.WebLinks.Add(foreignRow);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteWebLinkHandler(db)
                .Handle(new DeleteWebLinkCommand(foreignRow.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadyDeletedRow_ThrowsNotFound()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        var row = NewRow(CompanyA, "Verify Halal");
        db.WebLinks.Add(row);
        await db.SaveChangesAsync();

        await new DeleteWebLinkHandler(db)
            .Handle(new DeleteWebLinkCommand(row.Id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteWebLinkHandler(db)
                .Handle(new DeleteWebLinkCommand(row.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_KeepsTheIconMetadataOnTheSoftDeletedRow()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        var row = NewRow(CompanyA, "Verify Halal");
        db.WebLinks.Add(row);
        await db.SaveChangesAsync();
        var storageKey = row.Icon.StorageKey;

        await new DeleteWebLinkHandler(db)
            .Handle(new DeleteWebLinkCommand(row.Id), CancellationToken.None);

        // The bytes stay with the row, exactly like the CB logo on a soft-deleted CB.
        var stored = await db.WebLinks.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        Assert.Equal(storageKey, stored.Icon.StorageKey);
        Assert.Equal("icon.png", stored.Icon.FileName);
    }
}

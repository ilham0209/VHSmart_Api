using Microsoft.EntityFrameworkCore;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Persistence;

public class VHSmartDbContextTests
{
    [Fact]
    public async Task SaveChangesAsync_AddedEntity_StampsWithCurrentUser()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), new TestCurrentUser("user-123", Guid.NewGuid()));
        var before = DateTime.UtcNow.AddMinutes(-1);

        db.TestGlobalRecords.Add(new TestGlobalRecord { Name = "stamped" });
        await db.SaveChangesAsync();
        var saved = await db.TestGlobalRecords.SingleAsync();

        Assert.Equal("user-123", saved.SysUserCreated);
        Assert.True(saved.SysDateCreated >= before && saved.SysDateCreated <= DateTime.UtcNow.AddMinutes(1));
        Assert.False(saved.IsDeleted);
    }

    [Fact]
    public async Task SaveChangesAsync_AddedEntityWithoutUser_StampsWithSystem()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), new TestCurrentUser(string.Empty, Guid.NewGuid()));

        db.TestGlobalRecords.Add(new TestGlobalRecord { Name = "system row" });
        await db.SaveChangesAsync();
        var saved = await db.TestGlobalRecords.SingleAsync();

        Assert.Equal("system", saved.SysUserCreated);
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedEntity_StampsModifiedUserAndDate()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), new TestCurrentUser("user-123", Guid.NewGuid()));
        db.TestGlobalRecords.Add(new TestGlobalRecord { Name = "before" });
        await db.SaveChangesAsync();

        var entity = await db.TestGlobalRecords.SingleAsync();
        entity.Name = "after";
        await db.SaveChangesAsync();

        Assert.Equal("user-123", entity.SysUserModified);
        Assert.NotNull(entity.SysDateModified);
    }

    [Fact]
    public async Task SaveChangesAsync_DeletedEntity_SoftDeletesAndHidesRow()
    {
        var dbName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(dbName, new TestCurrentUser("user-123", Guid.NewGuid()));
        db.TestGlobalRecords.Add(new TestGlobalRecord { Name = "deletable" });
        await db.SaveChangesAsync();

        var entity = await db.TestGlobalRecords.SingleAsync();
        db.TestGlobalRecords.Remove(entity);
        await db.SaveChangesAsync();

        var verify = TestDbFactory.Create(dbName, new TestCurrentUser("user-456", Guid.NewGuid()));
        Assert.Null(await verify.TestGlobalRecords.SingleOrDefaultAsync());

        var hardDeletedRow = await verify.TestGlobalRecords.IgnoreQueryFilters().SingleAsync();
        Assert.True(hardDeletedRow.IsDeleted);
        Assert.Equal("user-123", hardDeletedRow.SysUserModified);
        Assert.NotNull(hardDeletedRow.SysDateModified);
    }

    [Fact]
    public async Task Query_TenantFilter_ReturnsOnlyCurrentCompanyRows()
    {
        var dbName = TestDbFactory.NewDatabaseName();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        var dbA = TestDbFactory.Create(dbName, new TestCurrentUser("user-a", companyA));
        dbA.TestTenantRecords.Add(new TestTenantRecord { CompanyId = companyA, Name = "A row" });
        dbA.TestTenantRecords.Add(new TestTenantRecord { CompanyId = companyB, Name = "B row" });
        await dbA.SaveChangesAsync();

        var dbB = TestDbFactory.Create(dbName, new TestCurrentUser("user-b", companyB));
        var rowsForB = await dbB.TestTenantRecords.ToListAsync();

        var single = Assert.Single(rowsForB);
        Assert.Equal("B row", single.Name);
    }

    [Fact]
    public async Task Query_PlatformAdmin_SeesRowsOfAllCompanies()
    {
        var dbName = TestDbFactory.NewDatabaseName();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        var dbA = TestDbFactory.Create(dbName, new TestCurrentUser("user-a", companyA));
        dbA.TestTenantRecords.Add(new TestTenantRecord { CompanyId = companyA, Name = "A row" });
        dbA.TestTenantRecords.Add(new TestTenantRecord { CompanyId = companyB, Name = "B row" });
        await dbA.SaveChangesAsync();

        var dbAdmin = TestDbFactory.Create(dbName, new TestCurrentUser("admin", Guid.NewGuid(), isPlatformAdmin: true));
        var allRows = await dbAdmin.TestTenantRecords.ToListAsync();

        Assert.Equal(2, allRows.Count);
    }
}

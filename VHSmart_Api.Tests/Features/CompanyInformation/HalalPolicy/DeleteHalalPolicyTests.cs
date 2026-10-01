using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.CompanyInformation.HalalPolicy;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.CompanyInformation.HalalPolicy;

public class DeleteHalalPolicyTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<HalalPolicyEntity> SeedRowAsync(TestableVHSmartDbContext db, Guid companyId)
    {
        var row = new HalalPolicyEntity
        {
            CompanyId = companyId,
            SchemeId = await db.Schemes.Select(scheme => scheme.Id).FirstAsync(),
            PolicyDate = new DateTime(2026, 1, 15),
            Document = new()
            {
                FileName = "policy.pdf",
                StorageKey = Guid.NewGuid().ToString("D"),
                ContentType = "application/pdf"
            }
        };
        db.HalalPolicies.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    [Fact]
    public async Task Handle_ExistingRow_SoftDeletesIt()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var row = await SeedRowAsync(db, CompanyA);

        await new DeleteHalalPolicyHandler(db, user)
            .Handle(new DeleteHalalPolicyCommand(row.Id), CancellationToken.None);

        var stored = await db.HalalPolicies.IgnoreQueryFilters().SingleAsync();
        Assert.True(stored.IsDeleted);
        // The filtered UQ (CompanyId, SchemeId) frees the scheme for a re-upload; the list
        // query filter hides the row.
        Assert.Equal(0, (await new GetAllHalalPoliciesHandler(db, user)
            .Handle(new GetAllHalalPoliciesQuery(), CancellationToken.None)).TotalRecords);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteHalalPolicyHandler(db, UserA())
                .Handle(new DeleteHalalPolicyCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CrossCompanyRow_ThrowsNotFoundAndKeepsTheRow()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var userA = UserA();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        var foreignRow = await SeedRowAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteHalalPolicyHandler(db, userA)
                .Handle(new DeleteHalalPolicyCommand(foreignRow.Id), CancellationToken.None));

        Assert.False((await db.HalalPolicies.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }
}

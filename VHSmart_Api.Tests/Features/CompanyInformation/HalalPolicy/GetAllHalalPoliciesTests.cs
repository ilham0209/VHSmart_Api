using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.CompanyInformation.HalalPolicy;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.CompanyInformation.HalalPolicy;

public class GetAllHalalPoliciesTests
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

    private static async Task<HalalPolicyEntity> NewRowAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string schemeName,
        string fileName,
        DateTime policyDate)
    {
        var schemeId = await db.Schemes
            .Where(scheme => scheme.Name == schemeName)
            .Select(scheme => scheme.Id)
            .FirstAsync();
        return new HalalPolicyEntity
        {
            CompanyId = companyId,
            SchemeId = schemeId,
            PolicyDate = policyDate,
            Document = new()
            {
                FileName = fileName,
                StorageKey = Guid.NewGuid().ToString("D"),
                ContentType = "application/pdf"
            }
        };
    }

    [Fact]
    public async Task Handle_SearchTerm_FindsSchemeNameOrFileName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeName = await db.Schemes.Select(scheme => scheme.Name).FirstAsync();
        db.HalalPolicies.AddRange(
            await NewRowAsync(db, CompanyA, schemeName, "first-policy.pdf", new DateTime(2026, 1, 1)),
            await NewRowAsync(db, CompanyA, schemeName, "annual-report.pdf", new DateTime(2026, 2, 1)));
        await db.SaveChangesAsync();

        var byFileName = await QueryAsync(db, user, "annual");
        var byScheme = await QueryAsync(db, user, schemeName);

        Assert.Equal("annual-report.pdf", Assert.Single(byFileName.Data).FileName);
        Assert.Equal(2, byScheme.TotalRecords);
    }

    [Fact]
    public async Task Handle_CrossCompanyRows_AreInvisible()
    {
        var userA = UserA();
        var userB = UserB();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        var schemeName = await db.Schemes.Select(scheme => scheme.Name).FirstAsync();
        db.HalalPolicies.AddRange(
            await NewRowAsync(db, CompanyA, schemeName, "company-a.pdf", new DateTime(2026, 1, 1)),
            await NewRowAsync(db, CompanyB, schemeName, "company-b.pdf", new DateTime(2026, 1, 2)));
        await db.SaveChangesAsync();

        var asCompanyA = await new GetAllHalalPoliciesHandler(db, userA)
            .Handle(new GetAllHalalPoliciesQuery(), CancellationToken.None);
        var asCompanyB = await new GetAllHalalPoliciesHandler(
                TestDbFactory.Create(databaseName, userB), userB)
            .Handle(new GetAllHalalPoliciesQuery(), CancellationToken.None);

        Assert.Equal("company-a.pdf", Assert.Single(asCompanyA.Data).FileName);
        Assert.Equal("company-b.pdf", Assert.Single(asCompanyB.Data).FileName);
    }

    [Fact]
    public async Task Handle_DefaultSort_IsNewestPolicyDateFirstAndNumberedFromOne()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeName = await db.Schemes.Select(scheme => scheme.Name).FirstAsync();
        db.HalalPolicies.AddRange(
            await NewRowAsync(db, CompanyA, schemeName, "older.pdf", new DateTime(2026, 1, 1)),
            await NewRowAsync(db, CompanyA, schemeName, "newer.pdf", new DateTime(2026, 6, 1)));
        await db.SaveChangesAsync();

        var result = await new GetAllHalalPoliciesHandler(db, user)
            .Handle(new GetAllHalalPoliciesQuery(), CancellationToken.None);

        Assert.Equal(["newer.pdf", "older.pdf"], result.Data.Select(row => row.FileName).ToArray());
        // "No." is the position in the whole list, starting at 1 (spec 7.2 first column).
        Assert.Equal([1, 2], result.Data.Select(row => row.No).ToArray());
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsNotListed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeName = await db.Schemes.Select(scheme => scheme.Name).FirstAsync();
        var row = await NewRowAsync(db, CompanyA, schemeName, "doomed.pdf", new DateTime(2026, 1, 1));
        db.HalalPolicies.Add(row);
        await db.SaveChangesAsync();
        db.HalalPolicies.Remove(row);
        await db.SaveChangesAsync();

        var result = await new GetAllHalalPoliciesHandler(db, user)
            .Handle(new GetAllHalalPoliciesQuery(), CancellationToken.None);

        Assert.Equal(0, result.TotalRecords);
    }

    private static Task<DataGridResponse<GetAllHalalPoliciesResponse>> QueryAsync(
        TestableVHSmartDbContext db,
        TestCurrentUser user,
        string searchTerm) =>
        new GetAllHalalPoliciesHandler(db, user).Handle(
            new GetAllHalalPoliciesQuery { Request = new DataGridRequest { SearchTerm = searchTerm } },
            CancellationToken.None);
}

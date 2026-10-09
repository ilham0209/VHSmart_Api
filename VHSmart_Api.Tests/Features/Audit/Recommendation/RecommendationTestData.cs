using VHSmart_Api.Shared.Domain.Audit;

namespace VHSmart_Api.Tests.Features.Audit.Recommendation;

// Shared fixtures for the Recommendation tests: two companies, their users, and the rows the
// handlers read. Every test works in its own in-memory database, so the shared company ids
// are safe to reuse.
internal static class RecommendationTestData
{
    public static readonly Guid CompanyA = Guid.NewGuid();

    public static readonly Guid CompanyB = Guid.NewGuid();

    public static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    public static TestCurrentUser UserB() => new(Guid.NewGuid().ToString(), CompanyB);

    public static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    public static async Task<Guid> SeedRecommendationAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Santan Berkualiti",
        string recommendationCode = "R111",
        string? description = null)
    {
        var row = new RecommendationEntity
        {
            CompanyId = companyId,
            Name = name,
            RecommendationCode = recommendationCode,
            Description = description
        };
        db.Recommendations.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }
}

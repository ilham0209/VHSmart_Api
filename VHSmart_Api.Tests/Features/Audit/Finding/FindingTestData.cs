using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;

namespace VHSmart_Api.Tests.Features.Audit.Finding;

// Shared fixtures for the Finding tests: two companies, their users, and the rows the
// handlers read. Every test works in its own in-memory database, so the shared company ids
// are safe to reuse.
internal static class FindingTestData
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

    public static async Task<Guid> SeedFindingAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Portion sizes are consistent with the menu descriptions.",
        string findingCode = "Portion sizes",
        string? description = null)
    {
        var row = new FindingEntity
        {
            CompanyId = companyId,
            Name = name,
            FindingCode = findingCode,
            Description = description
        };
        db.Findings.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task SeedFindingLinkAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid findingId,
        Guid recommendationId)
    {
        db.FindingRecommendations.Add(new FindingRecommendationEntity
        {
            CompanyId = companyId,
            FindingId = findingId,
            RecommendationId = recommendationId
        });
        await db.SaveChangesAsync();
    }

    // A login identity with a membership in companyId - the DeleteFinding notification
    // recipient query reads AdmUsers + AdmUserCompanies. Emails are unique per seed call
    // (spec 21.9: user e-mail unique across non-deleted users).
    public static async Task<Guid> SeedUserAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        bool isActive = true,
        Guid? userId = null)
    {
        var id = userId ?? Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = id,
            Name = "SEED USER",
            Email = $"seed-{Guid.NewGuid():N}@test.local",
            PasswordHash = "not-a-real-hash",
            IsActive = isActive,
            RoleId = Guid.NewGuid()
        });
        db.UserCompanies.Add(new UserCompanyEntity
        {
            UserId = id,
            CompanyId = companyId
        });
        await db.SaveChangesAsync();
        return id;
    }
}

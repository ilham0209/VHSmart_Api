namespace VHSmart_Api.Tests.Features.Support;

// Fixtures for the Support feedback card (DS-05): two companies; the submitter's id is a
// JWT string that the handler parses (SaveCorrectiveAction pattern).
internal static class SupportTestData
{
    public static readonly Guid CompanyA = Guid.NewGuid();

    public static readonly Guid CompanyB = Guid.NewGuid();

    public static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    public static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}

using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Tests.Features.Reference;

// Fixtures for the Reference menu (DS-04): two companies and a web-link seeder with an
// icon (a dummy storage key is enough for the list; the icon tests bring their own
// LocalFileStorage so the bytes are real).
internal static class ReferenceTestData
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

    public static async Task<Guid> SeedWebLinkAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name,
        string? description = null,
        StoredFile? icon = null,
        bool isDeleted = false)
    {
        var row = new WebLinkEntity
        {
            CompanyId = companyId,
            Name = name,
            Webpage = $"https://{name.ToLowerInvariant().Replace(' ', '-')}.example.org",
            Description = description,
            Icon = icon ?? new StoredFile
            {
                FileName = "icon.png",
                StorageKey = Guid.NewGuid().ToString("D"),
                ContentType = "image/png",
                SizeBytes = 4
            },
            IsDeleted = isDeleted
        };
        db.WebLinks.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }
}

using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;

namespace VHSmart_Api.Tests.Features.Audit.AuditPrefix;

// Shared fixtures for the Audit Prefix tests: two companies, their users, the COMPANY/Brand
// General Data rows the Brand dropdown offers, and the prefix rows the handlers read. Every
// test works in its own in-memory database, so the shared company ids are safe to reuse.
internal static class AuditPrefixTestData
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

    public static async Task<Guid> SeedBrandAsync(
        TestableVHSmartDbContext db,
        string name = "NATURAL",
        Guid? companyId = null)
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId ?? CompanyA,
            Group = GeneralDataGroup.COMPANY,
            Category = "Brand",
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A row of the caller's own company that is NOT a Brand - the validator must refuse it
    // for the Brand* field (spec 14.2), so this is the "wrong category" case.
    public static async Task<Guid> SeedNonBrandGeneralDataAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null)
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId ?? CompanyA,
            Group = GeneralDataGroup.COMPANY,
            Category = "Ownership Type",
            Name = "Sole Proprietor"
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedPrefixAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid? brandId = null,
        string prefix = "MRS",
        string? description = null)
    {
        var row = new AuditPrefixEntity
        {
            CompanyId = companyId,
            BrandId = brandId ?? await SeedBrandAsync(db, companyId: companyId),
            Prefix = prefix,
            Description = description
        };
        db.AuditPrefixes.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }
}

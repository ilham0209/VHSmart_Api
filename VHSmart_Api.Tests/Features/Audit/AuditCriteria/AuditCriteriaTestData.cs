using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;

namespace VHSmart_Api.Tests.Features.Audit.AuditCriteria;

// Shared fixtures for the Audit Criteria tests: two companies, their users, the AUDIT /
// "Internal - Audit Category" General Data rows the Category dropdown offers (the 14.1
// probable mapping the validator enforces), the Criteria / Sub Criteria master values, the
// findings the Finding Selection links, and the criteria rows themselves. Every test works
// in its own in-memory database, so the shared company ids are safe to reuse.
internal static class AuditCriteriaTestData
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

    // The Category dropdown value the validator accepts (spec 14.1 [VERIFY], question 36).
    public static async Task<Guid> SeedCategoryAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null,
        string name = "Kitchen")
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId ?? CompanyA,
            Group = GeneralDataGroup.AUDIT,
            Category = "Internal - Audit Category",
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // Same AUDIT group but the other category of the pair (spec 14.1 D-10 uses it for the
    // report screens) - the validator must refuse it for the Category* field.
    public static async Task<Guid> SeedExternalCategoryAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null)
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId ?? CompanyA,
            Group = GeneralDataGroup.AUDIT,
            Category = "External - Audit Category",
            Name = "External CB"
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedMasterAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        AuditCriteriaMasterKind kind = AuditCriteriaMasterKind.Criteria,
        string text = "Cleanliness")
    {
        var row = new AuditCriteriaMasterEntity
        {
            CompanyId = companyId,
            Kind = kind,
            Text = text
        };
        db.AuditCriteriaMasters.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedFindingAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Portion sizes are consistent with the menu descriptions.",
        string findingCode = "Portion sizes")
    {
        var row = new FindingEntity
        {
            CompanyId = companyId,
            Name = name,
            FindingCode = findingCode,
            Description = null
        };
        db.Findings.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedCriteriaAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid? categoryId = null,
        int categorySequence = 0,
        Guid? criteriaId = null,
        int criteriaSequence = 0,
        Guid? subCriteriaId = null,
        string? referenceCategory = null,
        string? reference = null,
        decimal potentialPoint = 1m,
        string? description = null)
    {
        var row = new AuditCriteriaEntity
        {
            CompanyId = companyId,
            CategoryId = categoryId ?? await SeedCategoryAsync(db, companyId),
            CategorySequence = categorySequence,
            CriteriaId = criteriaId ?? await SeedMasterAsync(db, companyId),
            CriteriaSequence = criteriaSequence,
            SubCriteriaId = subCriteriaId,
            ReferenceCategory = referenceCategory,
            Reference = reference,
            PotentialPoint = potentialPoint,
            Description = description
        };
        db.AuditCriteria.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task SeedCriteriaFindingLinkAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid auditCriteriaId,
        Guid findingId)
    {
        db.AuditCriteriaFindings.Add(new AuditCriteriaFindingEntity
        {
            CompanyId = companyId,
            AuditCriteriaId = auditCriteriaId,
            FindingId = findingId
        });
        await db.SaveChangesAsync();
    }
}

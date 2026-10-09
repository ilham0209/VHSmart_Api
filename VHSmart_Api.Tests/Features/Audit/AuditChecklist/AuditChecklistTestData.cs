using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;

namespace VHSmart_Api.Tests.Features.Audit.AuditChecklist;

// Shared fixtures for the Audit Checklist tests: two companies, their users, the AUDIT /
// "Internal - Audit Category" General Data rows the Checklist Category dropdown offers (the
// §14.1 probable mapping, flagged), the criteria rows the Criteria Selection ticks, the
// checklists themselves, and the audit plan rows that lock a checklist. Every test works in
// its own in-memory database, so the shared company ids are safe to reuse.
internal static class AuditChecklistTestData
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

    // The Checklist Category dropdown value the validator accepts (spec 14.1 [VERIFY] -
    // the same flag AU-04 carries).
    public static async Task<Guid> SeedChecklistCategoryAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null,
        string name = "Internal Supplier")
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

    // Same AUDIT group but the other category of the pair - refused for the Checklist
    // Category* field.
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

    // One full audit criteria row (its own category heading + criteria master), the unit
    // the Criteria Selection table lists and ticks.
    public static async Task<Guid> SeedCriteriaAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string categoryName = "Pest Control",
        string criteriaText = "Pest control schedule kept")
    {
        var category = new GeneralDataEntity
        {
            CompanyId = companyId,
            Group = GeneralDataGroup.AUDIT,
            Category = "Internal - Audit Category",
            Name = categoryName
        };
        var master = new AuditCriteriaMasterEntity
        {
            CompanyId = companyId,
            Kind = AuditCriteriaMasterKind.Criteria,
            Text = criteriaText
        };
        db.GeneralData.Add(category);
        db.AuditCriteriaMasters.Add(master);
        await db.SaveChangesAsync();

        var criteria = new AuditCriteriaEntity
        {
            CompanyId = companyId,
            CategoryId = category.Id,
            CategorySequence = 0,
            CriteriaId = master.Id,
            CriteriaSequence = 0,
            SubCriteriaId = null,
            ReferenceCategory = null,
            Reference = null,
            PotentialPoint = 1m,
            Description = null
        };
        db.AuditCriteria.Add(criteria);
        await db.SaveChangesAsync();
        return criteria.Id;
    }

    public static async Task<Guid> SeedChecklistAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid? categoryId = null,
        string name = "Checklist testing 18 Nov 25",
        string? description = null)
    {
        var row = new AuditChecklistEntity
        {
            CompanyId = companyId,
            ChecklistCategoryId = categoryId ?? await SeedChecklistCategoryAsync(db, companyId),
            Name = name,
            Description = description
        };
        db.AuditChecklists.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task SeedChecklistLinkAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid checklistId,
        Guid auditCriteriaId)
    {
        db.AuditChecklistCriteria.Add(new AuditChecklistCriteriaEntity
        {
            CompanyId = companyId,
            ChecklistId = checklistId,
            AuditCriteriaId = auditCriteriaId
        });
        await db.SaveChangesAsync();
    }

    // A live audit plan referencing the checklist - the computed lock of spec 14.6 reads
    // this table. The other FK columns point at ids no InMemory store validates (the lock
    // queries ChecklistId / CompanyId only; Audit Planning seeds its real principals).
    public static async Task<Guid> SeedPlanAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid checklistId)
    {
        var row = new AuditPlanEntity
        {
            CompanyId = companyId,
            AuditReferenceNo = $"REF-{Guid.NewGuid():N}",
            AuditPurposeId = Guid.NewGuid(),
            AuditTypeId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            PremiseId = Guid.NewGuid(),
            ScheduleDate = new DateTime(2026, 11, 18),
            GroupAuditorId = Guid.NewGuid(),
            ChecklistId = checklistId,
            AssignedByUserId = Guid.NewGuid(),
            DateAssigned = new DateTime(2026, 11, 1)
        };
        db.AuditPlans.Add(row);
        await db.SaveChangesAsync();
        // Detach: a live request scope never materialises plans, but a plan tracked here
        // would take part in the change tracker's relationship fixup when a test
        // soft-deletes its checklist (EF sees a severed required FK - the SQL store does
        // not, a soft delete only updates IsDeleted). Seeding must not change that.
        db.ChangeTracker.Clear();
        return row.Id;
    }
}

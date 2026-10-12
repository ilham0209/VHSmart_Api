using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Audit.NonConformanceList;

// Shared fixtures for the Non Conformance List tests: direct NC and corrective-action
// entry seeders plus the user row the history Fill By join needs. Every test works in its
// own in-memory database, so the shared company ids are safe to reuse.
internal static class NonConformanceTestData
{
    public static readonly Guid CompanyA = Guid.NewGuid();

    public static readonly Guid CompanyB = Guid.NewGuid();

    public static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    public static TestCurrentUser UserB() => new(Guid.NewGuid().ToString(), CompanyB);

    // A user whose id is not a Guid - the handlers' Guid.TryParse guard.
    public static TestCurrentUser InvalidIdUser() => new("not-a-guid", CompanyA);

    public static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        db.Companies.Add(new CompanyEntity { Id = CompanyA, Name = "HALAL CO" });
        db.Companies.Add(new CompanyEntity { Id = CompanyB, Name = "OTHER CO" });
        await db.SaveChangesAsync();
        return db;
    }

    // The AdmUsers row whose Name the Corrective Action History Fill By column shows.
    public static async Task<Guid> SeedUserAsync(
        TestableVHSmartDbContext db,
        Guid? id = null,
        string name = "AUDITEE ONE")
    {
        var user = new UserEntity
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Email = $"{Guid.NewGuid():N}@example.com",
            RoleId = RoleSeedData.AuditorRoleId,
            IsActive = true,
            ActivatedAt = DateTime.UtcNow
        };
        user.PasswordHash = UserPasswordHasher.Hash(user, "Passw0rd!");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    public static async Task<Guid> SeedCategoryAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null,
        string name = "Critical")
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId ?? CompanyA,
            Group = GeneralDataGroup.AUDIT,
            Category = "Non Compliance Category",
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A direct NC row without going through the feed or the AU-09 handlers - for the list,
    // detail, corrective-action and history tests.
    public static async Task<Guid> SeedNonConformanceAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        NonConformanceSource source = NonConformanceSource.External,
        string status = NonConformanceStatus.Open,
        bool isFinal = false,
        Guid? auditTaskItemId = null,
        Guid? categoryId = null,
        string? auditReferenceNo = null,
        string? findings = null,
        string? remarks = "Logged on upload",
        string? tags = null)
    {
        var row = new NonConformanceEntity
        {
            CompanyId = companyId,
            Source = source,
            AuditReferenceNo = auditReferenceNo,
            AuditTaskItemId = auditTaskItemId,
            ItemName = "Seri Rasa Factory",
            DetailName = "Rendang product",
            Reference = "REF-1",
            Clause = "Clause 2.1",
            Findings = findings,
            NonConformanceDetails = "Pest traces found",
            CategoryId = categoryId,
            Remarks = remarks,
            Tags = tags,
            Status = status,
            IsFinal = isFinal,
            ClosedAt = status == NonConformanceStatus.Closed ? DateTime.UtcNow : null
        };
        db.NonConformances.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedEntryAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid nonConformanceId,
        CorrectiveActionFilledBy filledBy,
        Guid filledByUserId,
        int version,
        string? details = null,
        string? comment = null,
        string? statusSet = null,
        string? attachmentFileName = null)
    {
        var row = new CorrectiveActionEntryEntity
        {
            CompanyId = companyId,
            NonConformanceId = nonConformanceId,
            FilledBy = filledBy,
            Details = details,
            Comment = comment,
            StatusSet = statusSet,
            Version = version,
            Attachment = attachmentFileName is null
                ? null
                : new StoredFile
                {
                    FileName = attachmentFileName,
                    StorageKey = $"{Guid.NewGuid():N}",
                    ContentType = "application/pdf",
                    SizeBytes = 128
                },
            FilledByUserId = filledByUserId
        };
        db.CorrectiveActionEntries.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // An audit task item linked NC - the internal feed's product, for the AuditTaskId and
    // FinalOnly list filters.
    public static async Task<(Guid TaskId, Guid ItemId)> SeedTaskWithItemAsync(
        TestableVHSmartDbContext db,
        Guid companyId)
    {
        var category = new GeneralDataEntity
        {
            CompanyId = companyId,
            Group = GeneralDataGroup.AUDIT,
            Category = "Internal - Audit Category",
            Name = "Technical Category"
        };
        db.GeneralData.Add(category);
        var checklist = new AuditChecklistEntity
        {
            CompanyId = companyId,
            ChecklistCategoryId = category.Id,
            Name = "Checklist tech"
        };
        db.AuditChecklists.Add(checklist);
        var plan = new AuditPlanEntity
        {
            CompanyId = companyId,
            AuditReferenceNo = "SRN/STORE01/18-11-2026(1)",
            AuditPurposeId = Guid.NewGuid(),
            AuditTypeId = Guid.NewGuid(),
            CategoryId = category.Id,
            PremiseId = Guid.NewGuid(),
            ScheduleDate = new DateTime(2026, 11, 18),
            GroupAuditorId = Guid.NewGuid(),
            ChecklistId = checklist.Id,
            AssignedByUserId = Guid.NewGuid(),
            DateAssigned = new DateTime(2026, 11, 1)
        };
        db.AuditPlans.Add(plan);
        var task = new AuditTaskEntity
        {
            CompanyId = companyId,
            AuditPlanId = plan.Id,
            Status = AuditTaskStatus.InProgress,
            AuditScore = 0m,
            FinalReviewScore = 0m
        };
        db.AuditTasks.Add(task);
        var item = new AuditTaskItemEntity
        {
            CompanyId = companyId,
            AuditTaskId = task.Id,
            Category = "PEST CONTROL",
            CategorySequence = 1,
            Criteria = "Clean floor",
            CriteriaSequence = 1,
            PotentialPoint = 2m,
            ActualPoint = 2m,
            IsConforming = false,
            Remarks = "Pest traces behind the chiller."
        };
        db.AuditTaskItems.Add(item);
        await db.SaveChangesAsync();
        return (task.Id, item.Id);
    }
}

using Microsoft.Extensions.Configuration;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Sequences;

namespace VHSmart_Api.Tests.Features.Audit.AuditPlanning;

// Shared fixtures for the Audit Planning tests: the General Data pairs of the spec 14.1
// probable mapping (flagged), a Brand + its Audit Prefix (D-09), premises, the checklist +
// group the plan copies its template from, and the plan rows the calendar / download read.
// Every test works in its own in-memory database, so the shared company ids are safe to
// reuse. The reference numbers come from the REAL ReferenceNumberGenerator so the D-09
// format is asserted, not mocked.
internal static class AuditPlanningTestData
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

    public static ReferenceNumberGenerator CreateGenerator(TestableVHSmartDbContext db)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        return new ReferenceNumberGenerator(db, configuration);
    }

    public static async Task<Guid> SeedAuditPurposeAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null,
        string name = "Routine Audit")
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId ?? CompanyA,
            Group = GeneralDataGroup.AUDIT,
            Category = "Audit Purpose",
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedAuditTypeAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null,
        string name = "Internal")
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId ?? CompanyA,
            Group = GeneralDataGroup.AUDIT,
            Category = "Audit Type",
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedPlanningCategoryAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null,
        string name = "PREMISE")
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

    // An AUDIT-pair row that is NOT one of the three planning dropdowns - refused by the
    // validators.
    public static async Task<Guid> SeedNonPlanningCategoryAsync(TestableVHSmartDbContext db)
    {
        var row = new GeneralDataEntity
        {
            CompanyId = CompanyA,
            Group = GeneralDataGroup.AUDIT,
            Category = "Non Compliance Category",
            Name = "Critical"
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedBrandAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null,
        string name = "SERUNAI")
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

    public static async Task<Guid> SeedPrefixAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid brandId,
        string prefix = "SRN")
    {
        var row = new AuditPrefixEntity
        {
            CompanyId = companyId,
            BrandId = brandId,
            Prefix = prefix
        };
        db.AuditPrefixes.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedChecklistAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null,
        string name = "Checklist tech")
    {
        var company = companyId ?? CompanyA;
        var category = new GeneralDataEntity
        {
            CompanyId = company,
            Group = GeneralDataGroup.AUDIT,
            Category = "Internal - Audit Category",
            Name = "Technical Category"
        };
        db.GeneralData.Add(category);
        var checklist = new AuditChecklistEntity
        {
            CompanyId = company,
            ChecklistCategoryId = category.Id,
            Name = name
        };
        db.AuditChecklists.Add(checklist);
        await db.SaveChangesAsync();
        return checklist.Id;
    }

    public static async Task<Guid> SeedAuditorAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "LEAD ONE")
    {
        var user = new UserEntity
        {
            Name = name,
            Email = $"{Guid.NewGuid():N}@example.com",
            RoleId = RoleSeedData.AuditorRoleId,
            IsActive = true,
            ActivatedAt = DateTime.UtcNow
        };
        user.PasswordHash = UserPasswordHasher.Hash(user, "Passw0rd!");
        db.Users.Add(user);
        db.UserCompanies.Add(new UserCompanyEntity
        {
            UserId = user.Id,
            CompanyId = companyId,
            IsDefault = true
        });
        await db.SaveChangesAsync();
        return user.Id;
    }

    public static async Task<Guid> SeedGroupAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid checklistId,
        Guid leaderUserId,
        string name = "TEAM ALPHA",
        GroupAuditorStatus status = GroupAuditorStatus.Active)
    {
        var group = new GroupAuditorEntity
        {
            CompanyId = companyId,
            Name = name,
            Status = status,
            ChecklistId = checklistId,
            LeaderUserId = leaderUserId
        };
        db.GroupAuditors.Add(group);
        db.GroupAuditorMembers.Add(new GroupAuditorMemberEntity
        {
            CompanyId = companyId,
            GroupAuditorId = group.Id,
            UserId = leaderUserId
        });
        await db.SaveChangesAsync();
        return group.Id;
    }

    public static async Task<Guid> SeedPremiseAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Seri Rasa Factory",
        string? storeCode = "STORE01",
        Guid? brandId = null)
    {
        var row = new PremiseEntity
        {
            CompanyId = companyId,
            PremiseType = PremiseType.Factory,
            Name = name,
            Email = $"{Guid.NewGuid():N}@premise.my",
            StoreCode = storeCode,
            BrandId = brandId,
            Address1 = "1 Jalan Verify",
            Address2 = "Taman Industri",
            Postcode = "40000",
            District = "Selangor",
            CountryId = ReferenceSeedData.MalaysiaCountryId,
            State = "Selangor",
            Telephone = "0312345678",
            Status = "ACTIVE"
        };
        db.Premises.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A plan (and its auto-created task) without going through the handler - for the
    // calendar / download tests.
    public static async Task<Guid> SeedPlanAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid premiseId,
        Guid groupAuditorId,
        Guid checklistId,
        Guid purposeId,
        Guid typeId,
        Guid categoryId,
        string referenceNo = "SRN/STORE01/18-11-2026(1)",
        DateTime? scheduleDate = null)
    {
        var plan = new AuditPlanEntity
        {
            CompanyId = companyId,
            AuditReferenceNo = referenceNo,
            AuditPurposeId = purposeId,
            AuditTypeId = typeId,
            CategoryId = categoryId,
            PremiseId = premiseId,
            ScheduleDate = scheduleDate ?? new DateTime(2026, 11, 18),
            GroupAuditorId = groupAuditorId,
            ChecklistId = checklistId,
            AssignedByUserId = Guid.NewGuid(),
            DateAssigned = new DateTime(2026, 11, 1)
        };
        db.AuditPlans.Add(plan);
        await db.SaveChangesAsync();
        return plan.Id;
    }
}

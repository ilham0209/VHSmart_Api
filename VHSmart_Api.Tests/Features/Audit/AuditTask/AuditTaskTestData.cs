using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Audit.AuditTask;

// Shared fixtures for the Audit Task tests: an Audit Criteria list (two categories, three
// rows, one Sub Criteria + a Reference) linked to a checklist, a group whose member is the
// audited company's user, a premise + plan + auto-shaped task, and direct item/task
// seeders for the answer/submit/final-review paths. Every test works in its own
// in-memory database, so the shared company ids are safe to reuse.
internal static class AuditTaskTestData
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

    public static async Task<Guid> SeedCategoryAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null,
        string name = "PEST CONTROL")
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

    public static async Task<Guid> SeedMasterAsync(
        TestableVHSmartDbContext db,
        string text,
        Guid? companyId = null,
        AuditCriteriaMasterKind kind = AuditCriteriaMasterKind.Criteria)
    {
        var row = new AuditCriteriaMasterEntity
        {
            CompanyId = companyId ?? CompanyA,
            Kind = kind,
            Text = text
        };
        db.AuditCriteriaMasters.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedCriteriaAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid categoryId,
        Guid criteriaMasterId,
        int categorySequence,
        int criteriaSequence,
        Guid? subCriteriaMasterId = null,
        string? reference = null,
        decimal potentialPoint = 1m)
    {
        var row = new AuditCriteriaEntity
        {
            CompanyId = companyId,
            CategoryId = categoryId,
            CategorySequence = categorySequence,
            CriteriaId = criteriaMasterId,
            CriteriaSequence = criteriaSequence,
            SubCriteriaId = subCriteriaMasterId,
            Reference = reference,
            PotentialPoint = potentialPoint
        };
        db.AuditCriteria.Add(row);
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

    public static async Task<Guid> SeedLinkAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid checklistId,
        Guid criteriaId)
    {
        var row = new AuditChecklistCriteriaEntity
        {
            CompanyId = companyId,
            ChecklistId = checklistId,
            AuditCriteriaId = criteriaId
        };
        db.AuditChecklistCriteria.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedAuditorAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "AUDITOR ONE")
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
        IReadOnlyList<Guid>? memberIds = null,
        string name = "TEAM ALPHA")
    {
        var group = new GroupAuditorEntity
        {
            CompanyId = companyId,
            Name = name,
            Status = GroupAuditorStatus.Active,
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
        foreach (var memberId in memberIds ?? [])
        {
            db.GroupAuditorMembers.Add(new GroupAuditorMemberEntity
            {
                CompanyId = companyId,
                GroupAuditorId = group.Id,
                UserId = memberId
            });
        }

        await db.SaveChangesAsync();
        return group.Id;
    }

    public static async Task<Guid> SeedPremiseAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Seri Rasa Factory",
        string? storeCode = "STORE01")
    {
        var row = new PremiseEntity
        {
            CompanyId = companyId,
            PremiseType = PremiseType.Factory,
            Name = name,
            Email = $"{Guid.NewGuid():N}@premise.my",
            StoreCode = storeCode,
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

    public static async Task<Guid> SeedPlanAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid premiseId,
        Guid groupAuditorId,
        Guid checklistId,
        Guid assignedByUserId,
        string referenceNo = "SRN/STORE01/18-11-2026(1)")
    {
        var plan = new AuditPlanEntity
        {
            CompanyId = companyId,
            AuditReferenceNo = referenceNo,
            AuditPurposeId = Guid.NewGuid(),
            AuditTypeId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            PremiseId = premiseId,
            ScheduleDate = new DateTime(2026, 11, 18),
            GroupAuditorId = groupAuditorId,
            ChecklistId = checklistId,
            AssignedByUserId = assignedByUserId,
            DateAssigned = new DateTime(2026, 11, 1)
        };
        db.AuditPlans.Add(plan);
        await db.SaveChangesAsync();
        return plan.Id;
    }

    public static async Task<Guid> SeedTaskAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid planId,
        AuditTaskStatus status = AuditTaskStatus.NotStarted)
    {
        var task = new AuditTaskEntity
        {
            CompanyId = companyId,
            AuditPlanId = planId,
            Status = status,
            AuditScore = 0m,
            FinalReviewScore = 0m
        };
        db.AuditTasks.Add(task);
        await db.SaveChangesAsync();
        return task.Id;
    }

    public static async Task<Guid> SeedTaskItemAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid taskId,
        int categorySequence,
        int criteriaSequence,
        decimal potentialPoint,
        decimal? actualPoint = null,
        string category = "PEST CONTROL",
        string criteria = "Clean floor",
        decimal? finalPoint = null,
        bool? isConforming = null,
        string? remarks = null)
    {
        var item = new AuditTaskItemEntity
        {
            CompanyId = companyId,
            AuditTaskId = taskId,
            Category = category,
            CategorySequence = categorySequence,
            Criteria = criteria,
            CriteriaSequence = criteriaSequence,
            PotentialPoint = potentialPoint,
            ActualPoint = actualPoint,
            FinalPoint = finalPoint,
            IsConforming = isConforming,
            Remarks = remarks
        };
        db.AuditTaskItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    // The standard stack used by most tests: the member user (who must also be the group
    // leader to keep the seed short - D-10 puts the leader among the members anyway), a
    // planner for the Assigned By join, a checklist with three criteria rows (cat 1 seq 1:
    // 1.1 potential 2 ref ISO 4.5 + sub criteria, 1.2 potential 3; cat 2 seq 2: 2.1
    // potential 1 - total potential 6), a premise, a plan and a task. Tasks that skip the
    // Start handler (InProgress/Completed) also get the three snapshot items with
    // ActualPoint = PotentialPoint, unless the test seeds its own (seedItems: false).
    public static async Task<(Guid TaskId, Guid PlanId, Guid GroupId, Guid ChecklistId, Guid PremiseId)>
        SeedStandardAuditAsync(
            TestableVHSmartDbContext db,
            Guid memberUserId,
            AuditTaskStatus taskStatus = AuditTaskStatus.NotStarted,
            Guid? companyId = null,
            bool seedItems = true)
    {
        var company = companyId ?? CompanyA;
        var categoryOne = await SeedCategoryAsync(db, company, "PEST CONTROL");
        var categoryTwo = await SeedCategoryAsync(db, company, "CLEANING");
        var masterOne = await SeedMasterAsync(db, "Clean floor", company);
        var masterTwo = await SeedMasterAsync(db, "No pest activity", company);
        var subMaster = await SeedMasterAsync(
            db, "Detailed records kept", company, AuditCriteriaMasterKind.SubCriteria);
        var criteriaOne = await SeedCriteriaAsync(
            db, company, categoryOne, masterOne, 1, 1,
            subCriteriaMasterId: subMaster, reference: "ISO 4.5", potentialPoint: 2m);
        var criteriaTwo = await SeedCriteriaAsync(
            db, company, categoryOne, masterTwo, 1, 2, potentialPoint: 3m);
        var criteriaThree = await SeedCriteriaAsync(
            db, company, categoryTwo, masterOne, 2, 1, potentialPoint: 1m);

        var checklist = await SeedChecklistAsync(db, company);
        await SeedLinkAsync(db, company, checklist, criteriaOne);
        await SeedLinkAsync(db, company, checklist, criteriaTwo);
        await SeedLinkAsync(db, company, checklist, criteriaThree);

        var group = await SeedGroupAsync(db, company, checklist, memberUserId);
        var planner = await SeedAuditorAsync(db, company, "PLANNER ONE");
        var premise = await SeedPremiseAsync(db, company);
        var plan = await SeedPlanAsync(db, company, premise, group, checklist, planner);
        var task = await SeedTaskAsync(db, company, plan, taskStatus);

        if (seedItems && taskStatus != AuditTaskStatus.NotStarted)
        {
            await SeedTaskItemAsync(
                db, company, task, 1, 1, 2m, actualPoint: 2m, category: "PEST CONTROL",
                criteria: "Clean floor");
            await SeedTaskItemAsync(
                db, company, task, 1, 2, 3m, actualPoint: 3m, category: "PEST CONTROL",
                criteria: "No pest activity");
            await SeedTaskItemAsync(
                db, company, task, 2, 1, 1m, actualPoint: 1m, category: "CLEANING",
                criteria: "Clean floor");
        }

        return (task, plan, group, checklist, premise);
    }
}

using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Audit.GroupAuditor;

// Shared fixtures for the Group Auditor tests: two companies, the caller identity, the
// AUDIT / "Internal - Audit Category" General Data rows the Template radio's category
// name reads (the AU-05 probable mapping, flagged), the Audit Checklists a group links as
// its single template, the auditor users (the seeded Auditor / Chief Auditor role - D-10
// resolves the spec 14.7 [VERIFY] source list), a wrong-role user for the role checks, and
// the groups with their member rows. Every test works in its own in-memory database, so
// the shared company ids are safe to reuse.
internal static class GroupAuditorTestData
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

    public static async Task<Guid> SeedChecklistAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid? categoryId = null,
        string name = "Checklist testing 18 Nov 25")
    {
        var row = new AuditChecklistEntity
        {
            CompanyId = companyId,
            ChecklistCategoryId = categoryId ?? await SeedChecklistCategoryAsync(db, companyId),
            Name = name
        };
        db.AuditChecklists.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // An auditor of a company (D-10): a user with the seeded Auditor / Chief Auditor role
    // and a membership row. Contact details feed the auditor table's columns.
    public static async Task<Guid> SeedAuditorAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "AUDITOR ONE",
        string? contactNo = "012-3456789",
        bool isActive = true)
    {
        var user = new UserEntity
        {
            Name = name,
            Email = $"{Guid.NewGuid():N}@example.com",
            RoleId = RoleSeedData.AuditorRoleId,
            IsActive = isActive,
            ActivatedAt = DateTime.UtcNow,
            ContactNo = contactNo
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

    // A company user who is NOT an auditor - refused by the member/leader role check.
    public static async Task<Guid> SeedNonAuditorAsync(
        TestableVHSmartDbContext db,
        Guid companyId)
    {
        var user = new UserEntity
        {
            Name = "ADMIN USER",
            Email = $"{Guid.NewGuid():N}@example.com",
            RoleId = RoleSeedData.VhSmartAdminRoleId,
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
        IReadOnlyList<Guid> memberIds,
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
        foreach (var memberId in memberIds.Distinct())
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
}

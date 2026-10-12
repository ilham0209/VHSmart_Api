using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Tests.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Audit.AuditTask;

// Real pipeline with an in-memory store and a permission stub that grants all four
// Audit.AuditTask actions (View/Create/Edit/Delete). The seeded member user's Id is the
// one the JWT carries - the group-membership visibility rule (spec 14.9 [MANUAL]) only
// shows tasks to group members, so the token must be theirs.
internal sealed class AuditTaskApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly Guid _grantedRoleId;
    private readonly bool _grantPermissions;
    private readonly string _databaseName = $"VHSmartAuditTaskApiTests-{Guid.NewGuid():N}";

    public AuditTaskApiFactory(Guid grantedRoleId, bool grantPermissions = true)
    {
        _grantedRoleId = grantedRoleId;
        _grantPermissions = grantPermissions;
    }

    public Guid CompanyId { get; } = Guid.NewGuid();

    public string CreateToken(Guid roleId, Guid? userId = null)
    {
        var parameters = Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme)
            .TokenValidationParameters;

        var key = parameters.IssuerSigningKey as SymmetricSecurityKey
            ?? throw new InvalidOperationException("Jwt:SigningKey did not reach the test host.");

        var token = new JwtSecurityToken(
            issuer: parameters.ValidIssuer,
            audience: parameters.ValidAudience,
            claims:
            [
                new Claim(JwtClaims.UserId, (userId ?? Guid.NewGuid()).ToString()),
                new Claim(JwtClaims.CompanyId, CompanyId.ToString()),
                new Claim(JwtClaims.RoleId, roleId.ToString()),
                new Claim(JwtClaims.IsPlatformAdmin, bool.FalseString),
                new Claim(JwtClaims.ViewAllCompanies, bool.FalseString)
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task SeedDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    // The full AU-08 stack under the factory's CompanyId: category + criteria masters,
    // three criteria rows (cat 1: 1.1 potential 2 ref ISO 4.5 + sub, 1.2 potential 3;
    // cat 2: 2.1 potential 1), checklist + links, the member user, the group (member =
    // leader), a premise, a plan and a NotStarted task.
    public async Task<(Guid TaskId, Guid MemberUserId, Guid ChecklistId)> SeedTaskAsync(
        AuditTaskStatus taskStatus = AuditTaskStatus.NotStarted)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        await db.Database.EnsureCreatedAsync();

        var categoryOne = new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.AUDIT,
            Category = "Internal - Audit Category",
            Name = "PEST CONTROL"
        };
        var categoryTwo = new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.AUDIT,
            Category = "Internal - Audit Category",
            Name = "CLEANING"
        };
        db.GeneralData.AddRange(categoryOne, categoryTwo);
        await db.SaveChangesAsync();

        var masterOne = new AuditCriteriaMasterEntity
        {
            CompanyId = CompanyId,
            Kind = AuditCriteriaMasterKind.Criteria,
            Text = "Clean floor"
        };
        var masterTwo = new AuditCriteriaMasterEntity
        {
            CompanyId = CompanyId,
            Kind = AuditCriteriaMasterKind.Criteria,
            Text = "No pest activity"
        };
        var subMaster = new AuditCriteriaMasterEntity
        {
            CompanyId = CompanyId,
            Kind = AuditCriteriaMasterKind.SubCriteria,
            Text = "Detailed records kept"
        };
        db.AuditCriteriaMasters.AddRange(masterOne, masterTwo, subMaster);
        await db.SaveChangesAsync();

        var criteriaOne = new AuditCriteriaEntity
        {
            CompanyId = CompanyId,
            CategoryId = categoryOne.Id,
            CategorySequence = 1,
            CriteriaId = masterOne.Id,
            CriteriaSequence = 1,
            SubCriteriaId = subMaster.Id,
            Reference = "ISO 4.5",
            PotentialPoint = 2m
        };
        var criteriaTwo = new AuditCriteriaEntity
        {
            CompanyId = CompanyId,
            CategoryId = categoryOne.Id,
            CategorySequence = 1,
            CriteriaId = masterTwo.Id,
            CriteriaSequence = 2,
            PotentialPoint = 3m
        };
        var criteriaThree = new AuditCriteriaEntity
        {
            CompanyId = CompanyId,
            CategoryId = categoryTwo.Id,
            CategorySequence = 2,
            CriteriaId = masterOne.Id,
            CriteriaSequence = 1,
            PotentialPoint = 1m
        };
        db.AuditCriteria.AddRange(criteriaOne, criteriaTwo, criteriaThree);
        await db.SaveChangesAsync();

        var checklistCategory = new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.AUDIT,
            Category = "Internal - Audit Category",
            Name = "Technical Category"
        };
        db.GeneralData.Add(checklistCategory);
        var checklist = new AuditChecklistEntity
        {
            CompanyId = CompanyId,
            ChecklistCategoryId = checklistCategory.Id,
            Name = "checklist tech"
        };
        db.AuditChecklists.Add(checklist);
        await db.SaveChangesAsync();
        db.AuditChecklistCriteria.AddRange(
            new AuditChecklistCriteriaEntity
            {
                CompanyId = CompanyId,
                ChecklistId = checklist.Id,
                AuditCriteriaId = criteriaOne.Id
            },
            new AuditChecklistCriteriaEntity
            {
                CompanyId = CompanyId,
                ChecklistId = checklist.Id,
                AuditCriteriaId = criteriaTwo.Id
            },
            new AuditChecklistCriteriaEntity
            {
                CompanyId = CompanyId,
                ChecklistId = checklist.Id,
                AuditCriteriaId = criteriaThree.Id
            });

        var member = new UserEntity
        {
            Name = "AUDITOR ONE",
            Email = $"{Guid.NewGuid():N}@example.com",
            RoleId = RoleSeedData.AuditorRoleId,
            IsActive = true,
            ActivatedAt = DateTime.UtcNow
        };
        member.PasswordHash = UserPasswordHasher.Hash(member, "Passw0rd!");
        db.Users.Add(member);
        db.UserCompanies.Add(new UserCompanyEntity
        {
            UserId = member.Id,
            CompanyId = CompanyId,
            IsDefault = true
        });

        var planner = new UserEntity
        {
            Name = "PLANNER ONE",
            Email = $"{Guid.NewGuid():N}@example.com",
            RoleId = RoleSeedData.AuditorRoleId,
            IsActive = true,
            ActivatedAt = DateTime.UtcNow
        };
        planner.PasswordHash = UserPasswordHasher.Hash(planner, "Passw0rd!");
        db.Users.Add(planner);
        await db.SaveChangesAsync();

        var group = new GroupAuditorEntity
        {
            CompanyId = CompanyId,
            Name = "TEAM ALPHA",
            Status = GroupAuditorStatus.Active,
            ChecklistId = checklist.Id,
            LeaderUserId = member.Id
        };
        db.GroupAuditors.Add(group);
        db.GroupAuditorMembers.Add(new GroupAuditorMemberEntity
        {
            CompanyId = CompanyId,
            GroupAuditorId = group.Id,
            UserId = member.Id
        });

        var premise = new PremiseEntity
        {
            CompanyId = CompanyId,
            PremiseType = PremiseType.Factory,
            Name = "Seri Rasa Factory",
            Email = $"{Guid.NewGuid():N}@premise.my",
            StoreCode = "STORE01",
            Address1 = "1 Jalan Verify",
            Address2 = "Taman Industri",
            Postcode = "40000",
            District = "Selangor",
            CountryId = ReferenceSeedData.MalaysiaCountryId,
            State = "Selangor",
            Telephone = "0312345678",
            Status = "ACTIVE"
        };
        db.Premises.Add(premise);
        await db.SaveChangesAsync();

        var plan = new AuditPlanEntity
        {
            CompanyId = CompanyId,
            AuditReferenceNo = "SRN/STORE01/18-11-2026(1)",
            AuditPurposeId = Guid.NewGuid(),
            AuditTypeId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            PremiseId = premise.Id,
            ScheduleDate = new DateTime(2026, 11, 18),
            GroupAuditorId = group.Id,
            ChecklistId = checklist.Id,
            AssignedByUserId = planner.Id,
            DateAssigned = new DateTime(2026, 11, 1)
        };
        db.AuditPlans.Add(plan);
        var task = new AuditTaskEntity
        {
            CompanyId = CompanyId,
            AuditPlanId = plan.Id,
            Status = taskStatus,
            AuditScore = 0m,
            FinalReviewScore = 0m
        };
        db.AuditTasks.Add(task);
        await db.SaveChangesAsync();

        return (task.Id, member.Id, checklist.Id);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<VHSmartDbContext>>();
            services.RemoveAll<DbContextOptions<VHSmartDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<VHSmartDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            var actions = new[] { PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Delete };
            var granted = _grantPermissions
                ? actions.Select(action => (_grantedRoleId, PermissionKeys.AuditAuditTask, action)).ToArray()
                : Array.Empty<(Guid, string, PermissionAction)>();

            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }
}

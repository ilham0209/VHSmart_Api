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
using VHSmart_Api.Shared.Infrastructure.Storage;
using VHSmart_Api.Tests.Features.Product.ManageProduct;
using VHSmart_Api.Tests.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Audit.NonConformanceList;

// Real pipeline with an in-memory store, a permission stub for Audit.NonConformance and an
// in-memory StubFileStorage so the corrective-action attachment route never touches a
// storage root. No group membership rule here (spec 14.11 is tenant-scoped): the token's
// company decides.
internal sealed class NonConformanceApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly Guid _grantedRoleId;
    private readonly bool _grantPermissions;
    private readonly string _databaseName = $"VHSmartNonConformanceApiTests-{Guid.NewGuid():N}";

    public NonConformanceApiFactory(Guid grantedRoleId, bool grantPermissions = true)
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

    // The company row so the list's constant Company Name cell resolves; the Non Compliance
    // Category; a plan + task + item stack so the fed internal NC has its reference columns;
    // one Open internal NC linked to the item; one Open external NC on the category; one
    // Closed internal NC (no item link) for the closed-state 409 paths. Ids come back for
    // the assertions.
    public async Task<(
        Guid CategoryId,
        Guid TaskId,
        Guid ItemId,
        Guid InternalNcId,
        Guid ExternalNcId,
        Guid ClosedNcId)> SeedFullStackAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        await db.Database.EnsureCreatedAsync();

        db.Companies.Add(new CompanyEntity { Id = CompanyId, Name = "HALAL CO" });
        var category = new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.AUDIT,
            Category = "Non Compliance Category",
            Name = "Critical"
        };
        db.GeneralData.Add(category);
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
            Name = "Checklist tech"
        };
        db.AuditChecklists.Add(checklist);
        var plan = new AuditPlanEntity
        {
            CompanyId = CompanyId,
            AuditReferenceNo = "SRN/STORE01/18-11-2026(1)",
            AuditPurposeId = Guid.NewGuid(),
            AuditTypeId = Guid.NewGuid(),
            CategoryId = checklistCategory.Id,
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
            CompanyId = CompanyId,
            AuditPlanId = plan.Id,
            Status = AuditTaskStatus.InProgress,
            AuditScore = 0m,
            FinalReviewScore = 0m
        };
        db.AuditTasks.Add(task);
        var item = new AuditTaskItemEntity
        {
            CompanyId = CompanyId,
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

        var internalNc = new NonConformanceEntity
        {
            CompanyId = CompanyId,
            Source = NonConformanceSource.Internal,
            AuditReferenceNo = "SRN/STORE01/18-11-2026(1)",
            AuditTaskItemId = item.Id,
            PremiseId = plan.PremiseId,
            Reference = "REF-1",
            Findings = "Pest traces behind the chiller.",
            Status = NonConformanceStatus.Open,
            IsFinal = false
        };
        var externalNc = new NonConformanceEntity
        {
            CompanyId = CompanyId,
            Source = NonConformanceSource.External,
            ItemName = "Seri Rasa Factory",
            DetailName = "Rendang product",
            Reference = "REF-1",
            Clause = "Clause 2.1",
            NonConformanceDetails = "Pest traces found",
            CategoryId = category.Id,
            Remarks = "Logged on upload",
            Status = NonConformanceStatus.Open,
            IsFinal = false
        };
        var closedNc = new NonConformanceEntity
        {
            CompanyId = CompanyId,
            Source = NonConformanceSource.Internal,
            AuditReferenceNo = "SRN/STORE01/18-11-2026(1)",
            Findings = "Already handled.",
            Status = NonConformanceStatus.Closed,
            ClosedAt = DateTime.UtcNow
        };
        db.NonConformances.AddRange(internalNc, externalNc, closedNc);
        await db.SaveChangesAsync();

        return (category.Id, task.Id, item.Id, internalNc.Id, externalNc.Id, closedNc.Id);
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

            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage>(new StubFileStorage());

            var actions = new[] { PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Delete };
            var granted = _grantPermissions
                ? actions.Select(action => (_grantedRoleId, PermissionKeys.AuditNonConformance, action)).ToArray()
                : Array.Empty<(Guid, string, PermissionAction)>();

            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }
}

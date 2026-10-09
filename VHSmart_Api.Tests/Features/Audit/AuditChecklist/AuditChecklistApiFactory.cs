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
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Tests.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Audit.AuditChecklist;

// Real pipeline with an in-memory store and a permission stub that grants all four
// Audit.AuditChecklist actions (View/Create/Edit/Delete). CompanyId is fixed per factory:
// the JWT carries it and the seeded rows carry it, so the tenant filter of a request sees
// its own data.
internal sealed class AuditChecklistApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly Guid _grantedRoleId;
    private readonly bool _grantPermissions;
    private readonly string _databaseName = $"VHSmartAuditChecklistApiTests-{Guid.NewGuid():N}";

    public AuditChecklistApiFactory(Guid grantedRoleId, bool grantPermissions = true)
    {
        _grantedRoleId = grantedRoleId;
        _grantPermissions = grantPermissions;
    }

    public Guid CompanyId { get; } = Guid.NewGuid();

    public string CreateToken(Guid roleId)
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
                new Claim(JwtClaims.UserId, Guid.NewGuid().ToString()),
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

    // Inserts rows directly under this factory's CompanyId (no HTTP context in the seed
    // scope, so the DbContext stamps "system").
    public async Task<Guid> SeedChecklistCategoryAsync(string name = "Internal Supplier")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.AUDIT,
            Category = "Internal - Audit Category",
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public async Task<Guid> SeedCriteriaAsync(string criteriaText = "Pest control schedule kept")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var category = new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.AUDIT,
            Category = "Internal - Audit Category",
            Name = "Pest Control"
        };
        var master = new AuditCriteriaMasterEntity
        {
            CompanyId = CompanyId,
            Kind = AuditCriteriaMasterKind.Criteria,
            Text = criteriaText
        };
        db.GeneralData.Add(category);
        db.AuditCriteriaMasters.Add(master);
        await db.SaveChangesAsync();

        var criteria = new AuditCriteriaEntity
        {
            CompanyId = CompanyId,
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

    public async Task<Guid> SeedChecklistAsync(
        Guid categoryId,
        string name = "Checklist testing 18 Nov 25",
        string? description = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new AuditChecklistEntity
        {
            CompanyId = CompanyId,
            ChecklistCategoryId = categoryId,
            Name = name,
            Description = description
        };
        db.AuditChecklists.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A live plan referencing the checklist - the computed lock of spec 14.6 (see
    // AuditChecklistTestData for the non-validated FK columns).
    public async Task SeedPlanAsync(Guid checklistId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        db.AuditPlans.Add(new AuditPlanEntity
        {
            CompanyId = CompanyId,
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
        });
        await db.SaveChangesAsync();
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
                ? actions.Select(action => (_grantedRoleId, PermissionKeys.AuditAuditChecklist, action)).ToArray()
                : Array.Empty<(Guid, string, PermissionAction)>();

            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }
}

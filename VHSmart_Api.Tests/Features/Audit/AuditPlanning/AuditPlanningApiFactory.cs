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

namespace VHSmart_Api.Tests.Features.Audit.AuditPlanning;

// Real pipeline with an in-memory store and a permission stub that grants all four
// Audit.AuditPlanning actions (View/Create/Edit/Delete). CompanyId is fixed per factory:
// the JWT carries it and the seeded rows carry it, so the tenant filter of a request sees
// its own data. The D-09 reference number is produced by the REAL ReferenceNumberGenerator
// resolved from the host DI.
internal sealed class AuditPlanningApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly Guid _grantedRoleId;
    private readonly bool _grantPermissions;
    private readonly string _databaseName = $"VHSmartAuditPlanningApiTests-{Guid.NewGuid():N}";

    public AuditPlanningApiFactory(Guid grantedRoleId, bool grantPermissions = true)
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
    public async Task<Guid> SeedDropdownsAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var purpose = new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.AUDIT,
            Category = "Audit Purpose",
            Name = "Routine Audit"
        };
        var type = new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.AUDIT,
            Category = "Audit Type",
            Name = "Internal"
        };
        var category = new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.AUDIT,
            Category = "Internal - Audit Category",
            Name = "PREMISE"
        };
        db.GeneralData.AddRange(purpose, type, category);
        await db.SaveChangesAsync();
        return purpose.Id;
    }

    // Scope reads without an HTTP context see no tenant (the filter reads the request's
    // company) - IgnoreQueryFilters + the fixed CompanyId assertion (the AU-05 precedent).
    public async Task<(Guid PurposeId, Guid TypeId, Guid CategoryId)> GetDropdownIdsAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var purpose = await db.GeneralData.IgnoreQueryFilters().FirstAsync(
            row => row.CompanyId == CompanyId && row.Category == "Audit Purpose");
        var type = await db.GeneralData.IgnoreQueryFilters().FirstAsync(
            row => row.CompanyId == CompanyId && row.Category == "Audit Type");
        var category = await db.GeneralData.IgnoreQueryFilters().FirstAsync(
            row => row.CompanyId == CompanyId && row.Category == "Internal - Audit Category");
        return (purpose.Id, type.Id, category.Id);
    }

    public async Task<(Guid PremiseId, Guid GroupId, Guid ChecklistId)> SeedPlanningTargetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();

        var brand = new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.COMPANY,
            Category = "Brand",
            Name = "SERUNAI"
        };
        db.GeneralData.Add(brand);
        await db.SaveChangesAsync();
        db.AuditPrefixes.Add(new AuditPrefixEntity
        {
            CompanyId = CompanyId,
            BrandId = brand.Id,
            Prefix = "SRN"
        });

        var premise = new PremiseEntity
        {
            CompanyId = CompanyId,
            PremiseType = PremiseType.Factory,
            Name = "Seri Rasa Factory",
            Email = $"{Guid.NewGuid():N}@premise.my",
            StoreCode = "STORE01",
            BrandId = brand.Id,
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

        var category = new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.AUDIT,
            Category = "Internal - Audit Category",
            Name = "Technical Category"
        };
        db.GeneralData.Add(category);
        var checklist = new AuditChecklistEntity
        {
            CompanyId = CompanyId,
            ChecklistCategoryId = category.Id,
            Name = "checklist tech"
        };
        db.AuditChecklists.Add(checklist);

        var leader = new UserEntity
        {
            Name = "LEAD ONE",
            Email = $"{Guid.NewGuid():N}@example.com",
            RoleId = RoleSeedData.AuditorRoleId,
            IsActive = true,
            ActivatedAt = DateTime.UtcNow
        };
        leader.PasswordHash = UserPasswordHasher.Hash(leader, "Passw0rd!");
        db.Users.Add(leader);
        db.UserCompanies.Add(new UserCompanyEntity
        {
            UserId = leader.Id,
            CompanyId = CompanyId,
            IsDefault = true
        });
        await db.SaveChangesAsync();

        var group = new GroupAuditorEntity
        {
            CompanyId = CompanyId,
            Name = "TEAM ALPHA",
            Status = GroupAuditorStatus.Active,
            ChecklistId = checklist.Id,
            LeaderUserId = leader.Id
        };
        db.GroupAuditors.Add(group);
        db.GroupAuditorMembers.Add(new GroupAuditorMemberEntity
        {
            CompanyId = CompanyId,
            GroupAuditorId = group.Id,
            UserId = leader.Id
        });
        await db.SaveChangesAsync();

        return (premise.Id, group.Id, checklist.Id);
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
                ? actions.Select(action => (_grantedRoleId, PermissionKeys.AuditAuditPlanning, action)).ToArray()
                : Array.Empty<(Guid, string, PermissionAction)>();

            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }
}

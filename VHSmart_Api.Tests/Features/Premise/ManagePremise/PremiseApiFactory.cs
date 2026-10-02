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
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Tests.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Premise.ManagePremise;

// Real pipeline with an in-memory store and a permission stub over the
// Premise.ManagePremise key. CompanyId is fixed per factory: the JWT carries it and the
// seeded rows carry it, so the tenant filter of a request sees its own data. The GENERAL
// Data rows (COMPANY / Brand, COMPANY / Prayer Room Availability) are NOT seeded by the
// app (Database.md 14) - the helpers insert them per factory company, the way a company
// super admin would through the General Data screen.
internal sealed class PremiseApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly bool _grantView;
    private readonly bool _grantCreate;
    private readonly bool _grantEdit;
    private readonly bool _grantDelete;
    private readonly string _databaseName = $"VHSmartPremiseApiTests-{Guid.NewGuid():N}";
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartPremiseApiTests", $"storage-{Guid.NewGuid():N}");

    public PremiseApiFactory(
        bool grantView = true,
        bool grantCreate = true,
        bool grantEdit = true,
        bool grantDelete = true)
    {
        _grantView = grantView;
        _grantCreate = grantCreate;
        _grantEdit = grantEdit;
        _grantDelete = grantDelete;
    }

    public Guid CompanyId { get; } = Guid.NewGuid();

    public string CreateToken()
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
                new Claim(JwtClaims.RoleId, RoleSeedData.VhSmartAdminRoleId.ToString()),
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

    // The company row behind this factory's CompanyId - the detail response joins it for
    // the read-only "For Company" cell (nothing seeds ComCompanies for these databases).
    public async Task<Guid> SeedCompanyAsync(string name = "Verify Halal Sdn Bhd")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new CompanyEntity
        {
            Id = CompanyId,
            Name = name,
            BusinessRegistrationNo = $"BRN-{Guid.NewGuid():N}",
            Address1 = "1 Jalan Verify",
            Address2 = string.Empty,
            PostCode = "50000",
            District = "Kuala Lumpur",
            State = "Kuala Lumpur",
            Telephone = "0312345678",
            Email = $"{Guid.NewGuid():N}@verify.my"
        };
        db.Companies.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // Another tenant's company + premise - the FKs need the company row even though its id
    // never matches the JWT.
    public async Task<Guid> SeedForeignCompanyAsync(string name = "Foreign Halal Sdn Bhd")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new CompanyEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            BusinessRegistrationNo = $"BRN-{Guid.NewGuid():N}",
            Address1 = "2 Jalan Foreign",
            Address2 = string.Empty,
            PostCode = "50000",
            District = "Johor Bahru",
            State = "Johor",
            Telephone = "0712345678",
            Email = $"{Guid.NewGuid():N}@verify.my"
        };
        db.Companies.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // The seeded country the premise form submits (R-01 lookup rows come from HasData).
    public async Task<Guid> MalaysiaCountryIdAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        return await db.Countries
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();
    }

    // A staff row under this factory's company - Premise Manager / Contact Person picks.
    // The id comes back directly: a bare scope has no JWT, so the tenant filter would hide
    // a re-query of it (same lesson as the IHCI helpers).
    public async Task<Guid> SeedStaffAsync(string name = "Siti Aminah")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();

        static async Task<Guid> AddPeopleValueAsync(
            VHSmartDbContext context, Guid companyId, string category, string name)
        {
            var existing = await context.GeneralData.IgnoreQueryFilters()
                .FirstOrDefaultAsync(row => !row.IsDeleted
                    && row.CompanyId == companyId
                    && row.Group == GeneralDataGroup.PEOPLE
                    && row.Category == category);
            if (existing is not null)
                return existing.Id;
            var row = new GeneralDataEntity
            {
                CompanyId = companyId,
                Group = GeneralDataGroup.PEOPLE,
                Category = category,
                Name = name
            };
            context.GeneralData.Add(row);
            await context.SaveChangesAsync();
            return row.Id;
        }

        var row = new StaffEntity
        {
            CompanyId = CompanyId,
            Name = name,
            Email = $"{Guid.NewGuid():N}@verify.my",
            MobileNumber = "0123456789",
            TitleId = await AddPeopleValueAsync(db, CompanyId, "Title of Honour", "Mr"),
            DepartmentId = await AddPeopleValueAsync(db, CompanyId, "Department", "Production"),
            DesignationId = await AddPeopleValueAsync(db, CompanyId, "Designation", "Halal Executive")
        };
        db.Staffs.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // COMPANY / Prayer Room Availability under this factory's company.
    public async Task<Guid> SeedPrayerRoomAsync(string name = "Available")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.COMPANY,
            Category = "Prayer Room Availability",
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A premise row - by default under this factory's company; the tests pass companyId for
    // another tenant's row (the FK needs that company to exist first).
    public async Task<Guid> SeedPremiseAsync(
        string name = "Seri Rasa Factory",
        string? email = null,
        string? storeCode = null,
        PremiseType premiseType = PremiseType.Factory,
        Guid? areaManagerStaffId = null,
        Guid? companyId = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var countryId = await db.Countries
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();
        var row = new PremiseEntity
        {
            CompanyId = companyId ?? CompanyId,
            PremiseType = premiseType,
            Name = name,
            Email = email ?? $"{Guid.NewGuid():N}@premise.my",
            StoreCode = storeCode,
            AreaManagerStaffId = areaManagerStaffId,
            Address1 = "1 Jalan Verify",
            Address2 = "Taman Industri",
            Postcode = "40000",
            City = "Shah Alam",
            District = "Selangor",
            CountryId = countryId,
            State = "Selangor",
            Telephone = "0312345678",
            Status = "ACTIVE"
        };
        db.Premises.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // Read back one premise name (a 404 test asserts the row was left untouched or
    // deleted). No JWT in this scope, so the tenant filter is bypassed - the id alone
    // decides visibility, and soft-deleted rows drop out through the manual IsDeleted check.
    public async Task<string?> PremiseNameAsync(Guid premiseId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        return await db.Premises
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(row => row.Id == premiseId && !row.IsDeleted)
            .Select(row => row.Name)
            .FirstOrDefaultAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("FileStorage:RootPath", _storageRoot);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<VHSmartDbContext>>();
            services.RemoveAll<DbContextOptions<VHSmartDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<VHSmartDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            var granted = new List<(Guid, string, PermissionAction)>();
            var roleId = RoleSeedData.VhSmartAdminRoleId;
            if (_grantView)
                granted.Add((roleId, PermissionKeys.PremiseManagePremise, PermissionAction.View));
            if (_grantCreate)
                granted.Add((roleId, PermissionKeys.PremiseManagePremise, PermissionAction.Create));
            if (_grantEdit)
                granted.Add((roleId, PermissionKeys.PremiseManagePremise, PermissionAction.Edit));
            if (_grantDelete)
                granted.Add((roleId, PermissionKeys.PremiseManagePremise, PermissionAction.Delete));

            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
    }
}

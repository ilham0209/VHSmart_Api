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
using VHSmart_Api.Shared.Infrastructure.Storage;
using VHSmart_Api.Tests.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Personnel.Trainings;

// Real pipeline with an in-memory store, a private file-storage root (removed on dispose) and
// a permission stub over the Personnel.InternalTraining key. CompanyId is fixed per factory:
// the JWT carries it and the seeded rows carry it, so the tenant filter of a request sees its
// own data. The General Data rows (PEOPLE + TRAINING / Module Type) are NOT seeded by the app
// (Database.md 14) - the helpers insert them per factory company, the way a company super
// admin would through the General Data screen.
internal sealed class TrainingApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly bool _grantView;
    private readonly bool _grantCreate;
    private readonly bool _grantEdit;
    private readonly bool _grantDelete;
    private readonly string _databaseName = $"VHSmartTrainingApiTests-{Guid.NewGuid():N}";
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartTrainingApiTests", $"storage-{Guid.NewGuid():N}");

    public TrainingApiFactory(
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

    // The company row behind this factory's CompanyId - needed where a test asserts the
    // Company Name cell (nothing seeds ComCompanies for these databases).
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

    // Another tenant's company - the FKs need the row even though its id never matches the JWT.
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

    // The PEOPLE values the staff form needs, for this factory's company. The ids come back
    // directly: a bare scope has no JWT, so the tenant filter would hide a re-query of them.
    public async Task<(Guid TitleId, Guid DepartmentId, Guid DesignationId, Guid IhcRoleId)>
        SeedStaffDataAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        return (
            await AddAsync(db, "Title of Honour", "Mr"),
            await AddAsync(db, "Department", "Production"),
            await AddAsync(db, "Designation", "Halal Executive"),
            await AddAsync(db, "Internal Halal Committee Role", "Member"));

        async Task<Guid> AddAsync(VHSmartDbContext context, string category, string name)
        {
            var existing = await context.GeneralData.IgnoreQueryFilters()
                .FirstOrDefaultAsync(row => !row.IsDeleted
                    && row.CompanyId == CompanyId
                    && row.Group == GeneralDataGroup.PEOPLE
                    && row.Category == category);
            if (existing is not null)
                return existing.Id;
            var row = new GeneralDataEntity
            {
                CompanyId = CompanyId,
                Group = GeneralDataGroup.PEOPLE,
                Category = category,
                Name = name
            };
            context.GeneralData.Add(row);
            await context.SaveChangesAsync();
            return row.Id;
        }
    }

    // General Data TRAINING / Module Type (spec 7.5 dropdown source).
    public async Task<Guid> SeedModuleTypeAsync(string name = "Presentation", Guid? companyId = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new GeneralDataEntity
        {
            CompanyId = companyId ?? CompanyId,
            Group = GeneralDataGroup.TRAINING,
            Category = "Module Type",
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A staff row (valid PEOPLE FKs) under this factory's company.
    public async Task<Guid> SeedStaffAsync(string name = "Siti Aminah", bool ihcMember = false)
    {
        var people = await SeedStaffDataAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();

        var row = new StaffEntity
        {
            CompanyId = CompanyId,
            Name = name,
            Email = $"{Guid.NewGuid():N}@verify.my",
            TitleId = people.TitleId,
            DepartmentId = people.DepartmentId,
            DesignationId = people.DesignationId,
            IsIhcMember = ihcMember,
            IhcRoleId = ihcMember ? people.IhcRoleId : null
        };
        db.Staffs.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A training with its attendee rows under this factory's company (or a company id chosen
    // by the test - the FK needs the company row to exist first).
    public async Task<Guid> SeedTrainingAsync(
        string name = "Halal Awareness 101",
        DateTime? date = null,
        TrainingType trainingType = TrainingType.AllStaff,
        IReadOnlyList<Guid>? attendees = null,
        Guid? companyId = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var training = new TrainingEntity
        {
            CompanyId = companyId ?? CompanyId,
            TrainingType = trainingType,
            Name = name,
            TrainingDate = date ?? new DateTime(2026, 3, 15)
        };
        db.Trainings.Add(training);
        foreach (var staffId in attendees ?? [])
        {
            db.TrainingAttendees.Add(new TrainingAttendeeEntity
            {
                CompanyId = companyId ?? CompanyId,
                TrainingId = training.Id,
                StaffId = staffId
            });
        }

        await db.SaveChangesAsync();
        return training.Id;
    }

    // A module row with real document bytes under an existing training row.
    public async Task<Guid> SeedModuleAsync(Guid trainingId, Guid moduleTypeId, string name = "Slide 1")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var document = await storage.SaveAsync(
            new MemoryStream([37, 80, 68, 70]), "module.pdf", "application/pdf");
        var row = new TrainingModuleEntity
        {
            CompanyId = CompanyId,
            TrainingId = trainingId,
            ModuleTypeId = moduleTypeId,
            ModuleName = name,
            Document = document
        };
        db.TrainingModules.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // Read back one training row (a 404 test asserts the row was left untouched). No JWT in
    // this scope, so the tenant filter is bypassed and the id alone decides visibility.
    public async Task<string?> TrainingNameAsync(Guid trainingId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        return await db.Trainings
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(row => row.Id == trainingId)
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
                granted.Add((roleId, PermissionKeys.PersonnelInternalTraining, PermissionAction.View));
            if (_grantCreate)
                granted.Add((roleId, PermissionKeys.PersonnelInternalTraining, PermissionAction.Create));
            if (_grantEdit)
                granted.Add((roleId, PermissionKeys.PersonnelInternalTraining, PermissionAction.Edit));
            if (_grantDelete)
                granted.Add((roleId, PermissionKeys.PersonnelInternalTraining, PermissionAction.Delete));

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

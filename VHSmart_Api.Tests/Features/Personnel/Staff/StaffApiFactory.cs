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

namespace VHSmart_Api.Tests.Features.Personnel.Staff;

// Real pipeline with an in-memory store, a private file-storage root (removed on dispose) and
// a permission stub that grants all four Personnel.AllStaff actions. CompanyId is fixed per
// factory: the JWT carries it and the seeded rows carry it, so the tenant filter of a request
// sees its own data. The General Data PEOPLE rows and the All Staff supporting-document type
// are NOT seeded by the app (Database.md 14) - the helpers insert them per factory company,
// the way a company super admin would through the General Data screen.
internal sealed class StaffApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly Guid _grantedRoleId;
    private readonly bool _grantPermissions;
    private readonly string _databaseName = $"VHSmartStaffApiTests-{Guid.NewGuid():N}";
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartStaffApiTests", $"storage-{Guid.NewGuid():N}");

    public StaffApiFactory(Guid grantedRoleId, bool grantPermissions = true)
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

    // The company row behind this factory's CompanyId - only needed where a test asserts the
    // Company Name cell (nothing seeds ComCompanies for these databases).
    public async Task SeedCompanyAsync(string name = "Verify Halal Sdn Bhd")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        db.Companies.Add(new CompanyEntity
        {
            Id = CompanyId,
            Name = name,
            BusinessRegistrationNo = "202600000001",
            Address1 = "1 Jalan Verify",
            Address2 = string.Empty,
            PostCode = "50000",
            District = "Kuala Lumpur",
            State = "Kuala Lumpur",
            Telephone = "0312345678",
            Email = "hq@verify.my"
        });
        await db.SaveChangesAsync();
    }

    // The four PEOPLE values the staff form needs (spec 7.4: Title*, Department*, Designation*,
    // IHC Role) for this factory's company.
    public async Task<(Guid TitleId, Guid DepartmentId, Guid DesignationId, Guid IhcRoleId)>
        SeedPeopleDataAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var title = await AddAsync(db, "Title of Honour", "Mr");
        var department = await AddAsync(db, "Department", "Production");
        var designation = await AddAsync(db, "Designation", "Halal Executive");
        var ihcRole = await AddAsync(db, "Internal Halal Committee Role", "Member");
        return (title, department, designation, ihcRole);

        async Task<Guid> AddAsync(VHSmartDbContext context, string category, string name)
        {
            var existing = await context.GeneralData
                .FirstOrDefaultAsync(row => row.CompanyId == CompanyId && row.Category == category);
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

    // A supporting-document type for the attachment dropdown (Database.md 3).
    public async Task<Guid> SeedDocumentTypeAsync(
        string documentType = "Medical Report",
        SupportingDocumentForView forView = SupportingDocumentForView.AllStaff)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new SupportingDocumentEntity
        {
            CompanyId = CompanyId,
            ForView = forView,
            DocumentType = documentType,
            DocumentSequence = 1,
            IsMandatory = false
        };
        db.SupportingDocuments.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // Inserts a staff row (valid FKs, no photo) directly under this factory's CompanyId.
    public async Task<Guid> SeedStaffAsync(
        string name = "Staff One",
        string email = "staff.one@verify.my",
        DateTime? typhoidExpiry = null)
    {
        var people = await SeedPeopleDataAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new StaffEntity
        {
            CompanyId = CompanyId,
            Email = email,
            TitleId = people.TitleId,
            Name = name,
            DepartmentId = people.DepartmentId,
            DesignationId = people.DesignationId,
            HasTyphoidInjection = typhoidExpiry is not null,
            TyphoidExpiryDate = typhoidExpiry
        };
        db.Staffs.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // Inserts an attachment with real document bytes under an existing staff row.
    public async Task<Guid> SeedAttachmentAsync(Guid staffId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var documentTypeId = await db.SupportingDocuments
            .Where(row => row.ForView == SupportingDocumentForView.AllStaff)
            .Select(row => row.Id)
            .FirstAsync();
        var document = await storage.SaveAsync(
            new MemoryStream([37, 80, 68, 70]), "report.pdf", "application/pdf");
        var row = new StaffAttachmentEntity
        {
            CompanyId = CompanyId,
            StaffId = staffId,
            DocumentTypeId = documentTypeId,
            Document = document
        };
        db.StaffAttachments.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
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

            var actions = new[] { PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Delete };
            var granted = _grantPermissions
                ? actions.Select(action => (_grantedRoleId, PermissionKeys.PersonnelAllStaff, action)).ToArray()
                : Array.Empty<(Guid, string, PermissionAction)>();

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

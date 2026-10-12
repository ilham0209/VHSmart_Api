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
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;
using VHSmart_Api.Tests.Features.Product.ManageProduct;
using VHSmart_Api.Tests.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Audit.ExternalReport;

// Real pipeline with an in-memory store, a permission stub for Audit.ExternalReport and an
// in-memory StubFileStorage so the attachment routes never touch a storage root. No group
// membership rule here (spec 14.10 is tenant-scoped): the token's company decides.
internal sealed class ExternalReportApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly Guid _grantedRoleId;
    private readonly bool _grantPermissions;
    private readonly string _databaseName = $"VHSmartExternalReportApiTests-{Guid.NewGuid():N}";

    public ExternalReportApiFactory(Guid grantedRoleId, bool grantPermissions = true)
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

    // The company row so the list's constant Company Name cell resolves; the D-10 reference
    // data (General Data pairs, a CB, a scheme); one premise; an opened report with one
    // premise link; one Open external NC with its Non Compliance Category; one PDF
    // attachment row. Ids come back for the assertions.
    public async Task<(
        Guid CertificationBodyId,
        Guid SchemeId,
        Guid TypeOfApplication,
        Guid TypeOfInspection,
        Guid NonComplianceCategoryId,
        Guid PremiseId,
        Guid ReportId,
        Guid NonConformanceId,
        Guid AttachmentId)> SeedFullStackAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        await db.Database.EnsureCreatedAsync();

        db.Companies.Add(new CompanyEntity { Id = CompanyId, Name = "HALAL CO" });

        var certificationBody = new CertificationBodyEntity
        {
            Name = "JABATAN KEMAJUAN ISLAM MALAYSIA (JAKIM)"
        };
        var scheme = new SchemeEntity { Name = "Halal Food", SortOrder = 1 };
        db.CertificationBodies.Add(certificationBody);
        db.Schemes.Add(scheme);
        await db.SaveChangesAsync();

        var typeOfApplication = new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.AUDIT,
            Category = "External - Audit Category",
            Name = "New Application"
        };
        var typeOfInspection = new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.AUDIT,
            Category = "External - Audit Reference",
            Name = "Surprise Inspection"
        };
        var nonComplianceCategory = new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.AUDIT,
            Category = "Non Compliance Category",
            Name = "Critical"
        };
        db.GeneralData.AddRange(typeOfApplication, typeOfInspection, nonComplianceCategory);
        await db.SaveChangesAsync();

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

        var report = new ExternalAuditReportEntity
        {
            CompanyId = CompanyId,
            ReceivedFromCertificationBodyId = certificationBody.Id,
            CbAuditReferenceNo = "HALAL-20251013-131025(PM)",
            TypeOfApplication = "New Application",
            TypeOfInspection = "Surprise Inspection",
            ApplicationSchemeId = scheme.Id,
            CbHalalApplicationNo = "APP-001",
            CertificationNo = "CERT-001",
            AuditDate = new DateTime(2025, 11, 29),
            TimeIn = new TimeSpan(9, 30, 0),
            TimeOut = new TimeSpan(12, 15, 0),
            Remarks = "Routine external visit",
            OverallStatus = "Pass",
            Status = ExternalAuditReportStatus.Opened
        };
        db.ExternalAuditReports.Add(report);
        db.ExternalAuditReportPremises.Add(new ExternalAuditReportPremiseEntity
        {
            CompanyId = CompanyId,
            ExternalAuditReportId = report.Id,
            PremiseId = premise.Id
        });

        var nonConformance = new NonConformanceEntity
        {
            CompanyId = CompanyId,
            Source = NonConformanceSource.External,
            ExternalAuditReportId = report.Id,
            ItemName = "Seri Rasa Factory",
            DetailName = "Rendang product",
            Reference = "REF-1",
            Clause = "Clause 2.1",
            NonConformanceDetails = "Pest traces found",
            CategoryId = nonComplianceCategory.Id,
            Remarks = "Logged on upload",
            Status = NonConformanceStatus.Open,
            IsFinal = false
        };
        db.NonConformances.Add(nonConformance);

        var attachment = new ExternalAuditReportAttachmentEntity
        {
            CompanyId = CompanyId,
            ExternalAuditReportId = report.Id,
            Document = new StoredFile
            {
                FileName = "report.pdf",
                StorageKey = $"{Guid.NewGuid():N}",
                ContentType = "application/pdf",
                SizeBytes = 128
            }
        };
        db.ExternalAuditReportAttachments.Add(attachment);
        await db.SaveChangesAsync();

        return (
            certificationBody.Id,
            scheme.Id,
            typeOfApplication.Id,
            typeOfInspection.Id,
            nonComplianceCategory.Id,
            premise.Id,
            report.Id,
            nonConformance.Id,
            attachment.Id);
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
                ? actions.Select(action => (_grantedRoleId, PermissionKeys.AuditExternalReport, action)).ToArray()
                : Array.Empty<(Guid, string, PermissionAction)>();

            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }
}

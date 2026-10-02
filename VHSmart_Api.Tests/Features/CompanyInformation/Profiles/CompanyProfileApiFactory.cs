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

namespace VHSmart_Api.Tests.Features.CompanyInformation.Profiles;

// Real pipeline (JWT -> subscription middleware -> [HasPermission]) with an in-memory store.
// The token's CompanyId and platform-admin flag are parameters: Company > Profiles is read by
// a super user (spec 7.3 list) and by a company user (own profile), so the test decides which
// of the two the request looks like. The permission stub grants only Company.Profiles, so a
// denied action proves the screen key gates the endpoint.
internal sealed class CompanyProfileApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly bool _grantView;
    private readonly bool _grantEdit;
    private readonly Guid _grantedRoleId = Guid.NewGuid();
    private readonly string _databaseName = $"VHSmartCompanyProfileApiTests-{Guid.NewGuid():N}";

    public CompanyProfileApiFactory(bool grantView = true, bool grantEdit = true)
    {
        _grantView = grantView;
        _grantEdit = grantEdit;
    }

    // The company the ordinary token belongs to.
    public Guid CompanyId { get; } = Guid.NewGuid();

    public string CreateToken(Guid companyId, bool isPlatformAdmin = false)
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
                new Claim(JwtClaims.CompanyId, companyId.ToString()),
                new Claim(JwtClaims.RoleId, _grantedRoleId.ToString()),
                new Claim(JwtClaims.IsPlatformAdmin, isPlatformAdmin.ToString()),
                new Claim(JwtClaims.ViewAllCompanies, isPlatformAdmin.ToString())
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

    // A company row with its own certification body (so the list has a CB name to show).
    public async Task<Guid> SeedCompanyAsync(string name = "Acme Foods", Guid? companyId = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var countryId = await db.Countries
            .AsNoTracking()
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();
        var certificationBody = new CertificationBodyEntity
        {
            Name = $"{name} CB",
            CountryId = countryId,
            City = "Kuala Lumpur",
            Postcode = "50000",
            State = "Wilayah Persekutuan",
            Telephone = "0380000000",
            Email = $"{Guid.NewGuid():N}@cb.example",
            ContactPerson = "Director"
        };
        db.CertificationBodies.Add(certificationBody);
        var company = new CompanyEntity
        {
            Id = companyId ?? Guid.NewGuid(),
            Name = name,
            CertificationBodyId = certificationBody.Id,
            RegistrationType = "Companies Commission Of Malaysia",
            BusinessRegistrationNo = $"BRN-{Guid.NewGuid():N}",
            OwnerStatus = "Muslim Owner",
            Address1 = "Jalan Perusahaan 1",
            Address2 = "Jalan Perusahaan 2",
            PostCode = "50000",
            District = "Gombak",
            State = "Selangor",
            CountryId = countryId,
            Telephone = "0300000000",
            Email = $"{Guid.NewGuid():N}@example.com"
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company.Id;
    }

    // A staff row under a company (valid PEOPLE FKs), for the profile's staff picker.
    public async Task<Guid> SeedStaffAsync(Guid companyId, string name = "Aiman Rahman")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var people = new GeneralDataEntity[]
        {
            new() { CompanyId = companyId, Group = GeneralDataGroup.PEOPLE, Category = "Title of Honour", Name = "Mr" },
            new() { CompanyId = companyId, Group = GeneralDataGroup.PEOPLE, Category = "Department", Name = "Production" },
            new() { CompanyId = companyId, Group = GeneralDataGroup.PEOPLE, Category = "Designation", Name = "Halal Executive" }
        };
        db.GeneralData.AddRange(people);
        var row = new StaffEntity
        {
            CompanyId = companyId,
            Name = name,
            Email = $"{Guid.NewGuid():N}@example.com",
            TitleId = people[0].Id,
            DepartmentId = people[1].Id,
            DesignationId = people[2].Id
        };
        db.Staffs.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
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

            var granted = new List<(Guid, string, PermissionAction)>();
            if (_grantView)
                granted.Add((_grantedRoleId, PermissionKeys.CompanyProfiles, PermissionAction.View));
            if (_grantEdit)
                granted.Add((_grantedRoleId, PermissionKeys.CompanyProfiles, PermissionAction.Edit));

            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }
}

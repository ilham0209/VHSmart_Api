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

namespace VHSmart_Api.Tests.Features.CompanyInformation.General;

// Real pipeline (JWT -> subscription middleware -> [HasPermission]) with an in-memory store.
// The token's CompanyId is a parameter: Company > General reads the caller's own company, so
// the test decides which tenant the request looks like. The permission stub grants only
// Company.General, so a denied action proves the screen key gates the endpoint.
internal sealed class CompanyGeneralApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly bool _grantView;
    private readonly bool _grantEdit;
    private readonly Guid _roleId = Guid.NewGuid();
    private readonly string _databaseName = $"VHSmartCompanyGeneralApiTests-{Guid.NewGuid():N}";

    public CompanyGeneralApiFactory(bool grantView = true, bool grantEdit = true)
    {
        _grantView = grantView;
        _grantEdit = grantEdit;
    }

    public string CreateToken(Guid companyId)
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
                new Claim(JwtClaims.RoleId, _roleId.ToString()),
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

    public async Task<Guid> SeedCompanyAsync(string name = "Acme Foods")
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
                granted.Add((_roleId, PermissionKeys.CompanyGeneral, PermissionAction.View));
            if (_grantEdit)
                granted.Add((_roleId, PermissionKeys.CompanyGeneral, PermissionAction.Edit));

            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }
}

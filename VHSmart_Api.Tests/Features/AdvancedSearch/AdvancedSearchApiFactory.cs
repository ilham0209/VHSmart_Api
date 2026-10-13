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

namespace VHSmart_Api.Tests.Features.AdvancedSearch;

// Real pipeline with an in-memory store and a permission stub over the AdvancedSearch key.
// CompanyId is fixed per factory: the JWT carries it and the seeded rows carry it, so the
// tenant filter of a request sees its own data. Two premises (one ACTIVE with an opening
// date, one INACTIVE) cover the basic search / filter / export responses.
internal sealed class AdvancedSearchApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly bool _grantView;
    private readonly string _databaseName = $"VHSmartAdvancedSearchApiTests-{Guid.NewGuid():N}";

    public AdvancedSearchApiFactory(bool grantView = true)
    {
        _grantView = grantView;
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

    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        await db.Database.EnsureCreatedAsync();

        var countryId = await db.Countries
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();

        db.Premises.Add(new PremiseEntity
        {
            CompanyId = CompanyId,
            PremiseType = PremiseType.Factory,
            Name = "Seri Rasa Factory",
            Email = $"{Guid.NewGuid():N}@premise.my",
            StoreCode = "SR-001",
            Address1 = "1 Jalan Verify",
            Address2 = "Taman Industri",
            Postcode = "40000",
            City = "Shah Alam",
            District = "Selangor",
            CountryId = countryId,
            State = "Selangor",
            Telephone = "0312345678",
            Status = "ACTIVE",
            OpeningDate = new DateTime(2026, 6, 15)
        });
        db.Premises.Add(new PremiseEntity
        {
            CompanyId = CompanyId,
            PremiseType = PremiseType.Factory,
            Name = "Kedai Kopi Dummy",
            Email = $"{Guid.NewGuid():N}@premise.my",
            StoreCode = "KO-002",
            Address1 = "2 Jalan Kopi",
            Address2 = "Taman Industri",
            Postcode = "40000",
            City = "Shah Alam",
            District = "Selangor",
            CountryId = countryId,
            State = "Johor",
            Telephone = "0312345678",
            Status = "INACTIVE"
        });
        db.GeneralData.Add(new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.COMPANY,
            Category = "Brand",
            Name = "Seri Rasa"
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

            var granted = new List<(Guid, string, PermissionAction)>();
            if (_grantView)
                granted.Add((
                    RoleSeedData.VhSmartAdminRoleId,
                    PermissionKeys.AdvancedSearch,
                    PermissionAction.View));
            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }
}

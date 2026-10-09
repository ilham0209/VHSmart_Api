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
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Tests.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;

// Real pipeline with an in-memory store and a permission stub that grants all four
// HalalApplication.ManageBatch actions. CompanyId defaults to one fixed value per factory (the
// JWT carries it and the seeded rows carry it, so the tenant filter sees its own data); a test
// may mint a token for another company to prove a foreign row stays invisible. The seeds run
// through the BatchTestData fixtures on a TestableVHSmartDbContext over the host's own options,
// so the factory and the handlers read the same store.
internal sealed class BatchApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly Guid _grantedRoleId;
    private readonly bool _grantPermissions;
    private readonly string _databaseName = $"VHSmartBatchApiTests-{Guid.NewGuid():N}";

    public BatchApiFactory(Guid grantedRoleId, bool grantPermissions = true)
    {
        _grantedRoleId = grantedRoleId;
        _grantPermissions = grantPermissions;
    }

    public Guid CompanyId { get; } = Guid.NewGuid();

    public string CreateToken(Guid roleId, Guid? companyId = null)
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
                new Claim(JwtClaims.CompanyId, (companyId ?? CompanyId).ToString()),
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

    // One seed unit on a Testable context over the host's registered options: same store, and
    // the BatchTestData fixtures (which need the testable type) can be reused verbatim.
    public async Task<T> SeedAsync<T>(Func<TestableVHSmartDbContext, Task<T>> run)
    {
        using var scope = Services.CreateScope();
        var db = new TestableVHSmartDbContext(
            scope.ServiceProvider.GetRequiredService<DbContextOptions<VHSmartDbContext>>(),
            new SystemCurrentUser());
        return await run(db);
    }

    public Task<Guid> SeedBatchAsync(
        string name = "Santan Batch Pertama",
        Guid? schemeId = null,
        Guid? brandId = null,
        Guid? companyId = null,
        Guid? manufacturerId = null) =>
        SeedAsync(db => BatchTestData.SeedBatchAsync(
            db, companyId ?? CompanyId, name,
            schemeId: schemeId, brandId: brandId, manufacturerId: manufacturerId));

    public Task<Guid> SeedBrandAsync(string name = "Sereni", Guid? companyId = null) =>
        SeedAsync(db => BatchTestData.SeedBrandAsync(db, name, companyId ?? CompanyId));

    public Task<Guid> SeedManufacturerAsync(string name = "Santan Foods", Guid? companyId = null) =>
        SeedAsync(db => BatchTestData.SeedManufacturerAsync(db, companyId ?? CompanyId, name));

    public Task<Guid> ProductSchemeIdAsync() => SeedAsync(BatchTestData.ProductSchemeIdAsync);

    public Task<Guid> FoodPremiseSchemeIdAsync() =>
        SeedAsync(BatchTestData.FoodPremiseSchemeIdAsync);

    public Task<Guid> SeedValidProductAsync(string name = "Santan Kicap", Guid? companyId = null) =>
        SeedAsync(db => BatchTestData.SeedValidProductAsync(db, companyId ?? CompanyId, name));

    public Task<Guid> SeedExpiredProductAsync(
        string name = "Santan Kicap Tengo",
        Guid? companyId = null) =>
        SeedAsync(db => BatchTestData.SeedExpiredProductAsync(db, companyId ?? CompanyId, name));

    public Task<Guid> SeedCompletePremiseAsync(
        string name = "Seri Rasa Factory",
        Guid? companyId = null) =>
        SeedAsync(db => BatchTestData.SeedCompletePremiseAsync(db, companyId ?? CompanyId, name));

    public Task<Guid> SeedIncompletePremiseAsync(
        string name = "Kedai Kopi Dummy",
        Guid? companyId = null) =>
        SeedAsync(db => BatchTestData.SeedIncompletePremiseAsync(db, companyId ?? CompanyId, name));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<VHSmartDbContext>>();
            services.RemoveAll<DbContextOptions<VHSmartDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<VHSmartDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            var actions = new[]
            {
                PermissionAction.View, PermissionAction.Create,
                PermissionAction.Edit, PermissionAction.Delete
            };
            var granted = _grantPermissions
                ? actions.Select(action =>
                    (_grantedRoleId, PermissionKeys.HalalApplicationManageBatch, action)).ToArray()
                : Array.Empty<(Guid, string, PermissionAction)>();

            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }
}

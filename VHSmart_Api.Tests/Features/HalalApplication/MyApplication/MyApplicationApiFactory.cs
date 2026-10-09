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
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Tests.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

// Real pipeline over an in-memory store with a permission stub for the
// HalalApplication.MyApplication key. Granted actions are configurable so a View-only role
// can be asserted against the POST. Seeds run on a TestableVHSmartDbContext over the host's
// own options, so the factory and the handlers read the same store - the same shape as
// BatchApiFactory, with this screen's fixtures.
internal sealed class MyApplicationApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly Guid _grantedRoleId;
    private readonly PermissionAction[] _grantedActions;
    private readonly string _databaseName = $"VHSmartMyApplicationApiTests-{Guid.NewGuid():N}";

    public MyApplicationApiFactory(
        Guid grantedRoleId,
        PermissionAction[]? grantedActions = null)
    {
        _grantedRoleId = grantedRoleId;
        _grantedActions = grantedActions ??
        [
            PermissionAction.View, PermissionAction.Create,
            PermissionAction.Edit, PermissionAction.Delete
        ];
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

    public async Task<T> SeedAsync<T>(Func<TestableVHSmartDbContext, Task<T>> run)
    {
        using var scope = Services.CreateScope();
        var db = new TestableVHSmartDbContext(
            scope.ServiceProvider.GetRequiredService<DbContextOptions<VHSmartDbContext>>(),
            new SystemCurrentUser());
        return await run(db);
    }

    public Task SeedCompanyAsync(string name = "Sereni Trading Sdn Bhd", Guid? companyId = null) =>
        SeedAsync(db =>
        {
            db.Companies.Add(new CompanyEntity
            {
                Id = companyId ?? CompanyId,
                Name = name
            });
            return db.SaveChangesAsync();
        });

    public Task<Guid> SeedApplicationAsync(
        string referenceNo = "VHS(PR)/01012026/1",
        Guid? companyId = null) =>
        SeedAsync(db => ApplicationTestData.SeedApplicationAsync(
            db, companyId ?? CompanyId, referenceNo: referenceNo));

    public Task<Guid> ProductSchemeIdAsync() =>
        SeedAsync(ApplicationTestData.ProductSchemeIdAsync);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<VHSmartDbContext>>();
            services.RemoveAll<DbContextOptions<VHSmartDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<VHSmartDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            var granted = _grantedActions
                .Select(action =>
                    (_grantedRoleId, PermissionKeys.HalalApplicationMyApplication, action))
                .ToArray();

            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }
}

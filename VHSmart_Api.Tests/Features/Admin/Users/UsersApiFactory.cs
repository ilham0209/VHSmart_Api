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

namespace VHSmart_Api.Tests.Features.Admin.Users;

// Real pipeline (JWT -> JwtCurrentUser -> [HasPermission] -> MediatR -> UserController) with
// SQL Server swapped for a private in-memory store and IPermissionService stubbed, so the
// permission gate and the data scope (platform admin vs company admin) can be varied
// independently. Signing key is generated per factory (D-29: no secret in source).
internal sealed class UsersApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly Guid _grantedRoleId;
    private readonly bool _grantPermissions;
    private readonly string _databaseName = $"VHSmartUserApiTests-{Guid.NewGuid():N}";

    public UsersApiFactory(Guid grantedRoleId, bool grantPermissions = true)
    {
        _grantedRoleId = grantedRoleId;
        _grantPermissions = grantPermissions;
    }

    public string CreateToken(
        Guid userId,
        Guid companyId,
        bool isPlatformAdmin = true)
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
                new Claim(JwtClaims.UserId, userId.ToString()),
                new Claim(JwtClaims.CompanyId, companyId.ToString()),
                new Claim(JwtClaims.RoleId, _grantedRoleId.ToString()),
                new Claim(
                    JwtClaims.IsPlatformAdmin,
                    isPlatformAdmin ? bool.TrueString : bool.FalseString),
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);

        builder.ConfigureTestServices(services =>
        {
            // Tests never touch a real database. Both EF registrations have to be removed
            // first, otherwise the SqlServer and the InMemory provider end up on one context.
            services.RemoveAll<IDbContextOptionsConfiguration<VHSmartDbContext>>();
            services.RemoveAll<DbContextOptions<VHSmartDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<VHSmartDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            var actions = _grantPermissions
                ? new[]
                {
                    PermissionAction.View,
                    PermissionAction.Create,
                    PermissionAction.Edit,
                    PermissionAction.Delete
                }
                : Array.Empty<PermissionAction>();

            var granted = actions.Select(action => (_grantedRoleId, PermissionKeys.AdminUsers, action));
            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Security;

public class JwtCurrentUserTests
{
    [Fact]
    public void UserId_AuthenticatedWithSubClaim_ReturnsClaimValue()
    {
        var user = CreateUser(sub: "user-123");

        Assert.Equal("user-123", user.UserId);
    }

    [Fact]
    public void UserId_AuthenticatedWithMappedNameIdentifier_ReturnsClaimValue()
    {
        // JwtSecurityTokenHandler renames "sub" to ClaimTypes.NameIdentifier by default.
        var user = CreateUser(sub: null, nameIdentifier: "mapped-user");

        Assert.Equal("mapped-user", user.UserId);
    }

    [Fact]
    public void Claims_AuthenticatedWithClaims_ReturnsClaimValues()
    {
        var companyId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = CreateUser(
            sub: "user-123",
            companyId: companyId,
            roleId: roleId,
            isPlatformAdmin: true,
            viewAllCompanies: true);

        Assert.Equal(companyId, user.CompanyId);
        Assert.Equal(roleId, user.RoleId);
        Assert.True(user.IsPlatformAdmin);
        Assert.True(user.ViewAllCompanies);
    }

    [Fact]
    public void Claims_NoHttpContext_ReturnsFailClosedDefaults()
    {
        var user = new JwtCurrentUser(new HttpContextAccessor());

        Assert.Equal(string.Empty, user.UserId);
        Assert.Equal(Guid.Empty, user.CompanyId);
        Assert.Equal(Guid.Empty, user.RoleId);
        Assert.False(user.IsPlatformAdmin);
        Assert.False(user.ViewAllCompanies);
    }

    [Fact]
    public void Claims_UnauthenticatedPrincipal_IgnoresClaims()
    {
        var user = CreateUser(sub: "spoofed", companyId: Guid.NewGuid(), authenticated: false);

        Assert.Equal(string.Empty, user.UserId);
        Assert.Equal(Guid.Empty, user.CompanyId);
        Assert.False(user.IsPlatformAdmin);
        Assert.False(user.ViewAllCompanies);
    }

    [Fact]
    public void CompanyId_MalformedClaim_ReturnsEmptyGuid()
    {
        var user = CreateUser(sub: "user-123", companyIdClaim: "not-a-guid");

        Assert.Equal(Guid.Empty, user.CompanyId);
    }

    [Fact]
    public async Task Query_TenantFilter_JwtCompanyA_DoesNotSeeCompanyB()
    {
        var dbName = TestDbFactory.NewDatabaseName();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        var dbA = TestDbFactory.Create(dbName, CreateUser(sub: "user-a", companyId: companyA));
        dbA.TestTenantRecords.Add(new TestTenantRecord { CompanyId = companyA, Name = "A row" });
        dbA.TestTenantRecords.Add(new TestTenantRecord { CompanyId = companyB, Name = "B row" });
        await dbA.SaveChangesAsync();

        var dbB = TestDbFactory.Create(dbName, CreateUser(sub: "user-b", companyId: companyB));
        var rowsForB = await dbB.TestTenantRecords.ToListAsync();

        var single = Assert.Single(rowsForB);
        Assert.Equal("B row", single.Name);
    }

    [Fact]
    public async Task Query_PlatformAdminClaim_SeesRowsOfBothCompanies()
    {
        var allRows = await SeedTwoCompaniesAndReadAs(isPlatformAdmin: true, viewAllCompanies: false);

        Assert.Equal(2, allRows.Count);
    }

    [Fact]
    public async Task Query_ViewAllCompaniesClaim_SeesRowsOfBothCompanies()
    {
        var allRows = await SeedTwoCompaniesAndReadAs(isPlatformAdmin: false, viewAllCompanies: true);

        Assert.Equal(2, allRows.Count);
    }

    private static async Task<List<TestTenantRecord>> SeedTwoCompaniesAndReadAs(
        bool isPlatformAdmin,
        bool viewAllCompanies)
    {
        var dbName = TestDbFactory.NewDatabaseName();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        var dbA = TestDbFactory.Create(dbName, CreateUser(sub: "user-a", companyId: companyA));
        dbA.TestTenantRecords.Add(new TestTenantRecord { CompanyId = companyA, Name = "A row" });
        dbA.TestTenantRecords.Add(new TestTenantRecord { CompanyId = companyB, Name = "B row" });
        await dbA.SaveChangesAsync();

        var admin = TestDbFactory.Create(
            dbName,
            CreateUser(
                sub: "admin",
                companyId: Guid.NewGuid(),
                isPlatformAdmin: isPlatformAdmin,
                viewAllCompanies: viewAllCompanies));

        return await admin.TestTenantRecords.ToListAsync();
    }

    private static JwtCurrentUser CreateUser(
        string? sub = null,
        Guid? companyId = null,
        Guid? roleId = null,
        string? companyIdClaim = null,
        string? nameIdentifier = null,
        bool isPlatformAdmin = false,
        bool viewAllCompanies = false,
        bool authenticated = true)
    {
        var claims = new List<Claim>();
        if (sub is not null)
            claims.Add(new Claim(JwtClaims.UserId, sub));
        if (nameIdentifier is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, nameIdentifier));
        if (companyId is not null)
            claims.Add(new Claim(JwtClaims.CompanyId, companyId.Value.ToString()));
        if (companyIdClaim is not null)
            claims.Add(new Claim(JwtClaims.CompanyId, companyIdClaim));
        if (roleId is not null)
            claims.Add(new Claim(JwtClaims.RoleId, roleId.Value.ToString()));
        if (isPlatformAdmin)
            claims.Add(new Claim(JwtClaims.IsPlatformAdmin, bool.TrueString));
        if (viewAllCompanies)
            claims.Add(new Claim(JwtClaims.ViewAllCompanies, bool.TrueString));

        var identity = new ClaimsIdentity(claims, authenticated ? "Test" : null);
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return new JwtCurrentUser(accessor);
    }
}

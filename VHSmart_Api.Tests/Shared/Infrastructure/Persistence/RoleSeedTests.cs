using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Persistence;

public class RoleSeedTests
{
    private static async Task<TestableVHSmartDbContext> CreateSeededDatabaseAsync()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public async Task EnsureCreated_SeedsThreeSystemRoles()
    {
        var db = await CreateSeededDatabaseAsync();

        var roles = await db.Roles.OrderBy(role => role.Name).ToListAsync();

        Assert.Equal(
            ["Auditor / Chief Auditor", "Restaurant / Premise Manager", "VH Smart Admin"],
            roles.Select(role => role.Name));
        Assert.All(roles, role =>
        {
            Assert.True(role.IsSystemRole);
            Assert.Null(role.CompanyId);
        });
    }

    [Fact]
    public async Task EnsureCreated_VhSmartAdmin_HasEveryScreenWithEveryAction()
    {
        var db = await CreateSeededDatabaseAsync();

        var rows = await db.RolePermissions
            .Where(row => row.RoleId == RoleSeedData.VhSmartAdminRoleId)
            .ToListAsync();

        Assert.Equal(PermissionKeys.All.Count, rows.Count);
        Assert.Equal(PermissionKeys.All.OrderBy(key => key), rows.Select(row => row.PermissionKey).OrderBy(key => key));
        Assert.All(rows, row =>
        {
            Assert.True(row.CanView);
            Assert.True(row.CanCreate);
            Assert.True(row.CanEdit);
            Assert.True(row.CanDelete);
        });
    }

    [Fact]
    public async Task EnsureCreated_AuditorRole_HasEveryAuditScreenWithEveryAction()
    {
        var db = await CreateSeededDatabaseAsync();

        var rows = await db.RolePermissions
            .Where(row => row.RoleId == RoleSeedData.AuditorRoleId)
            .ToListAsync();

        var auditKeys = PermissionKeys.All
            .Where(key => key.StartsWith("Audit.", StringComparison.Ordinal))
            .ToArray();

        foreach (var key in auditKeys)
        {
            var row = Assert.Single(rows, candidate => candidate.PermissionKey == key);
            Assert.True(row.CanView);
            Assert.True(row.CanCreate);
            Assert.True(row.CanEdit);
            Assert.True(row.CanDelete);
        }

        // View on the lookups the audit screens read (reference data, premises) plus the two
        // personal screens, and nothing else outside Audit.*.
        Assert.Equal(auditKeys.Length + 4, rows.Count);
        foreach (var lookupKey in new[]
                 {
                     PermissionKeys.Dashboard,
                     PermissionKeys.AccountSetting,
                     PermissionKeys.AdminGeneralData,
                     PermissionKeys.PremiseManagePremise
                 })
        {
            var row = Assert.Single(rows, candidate => candidate.PermissionKey == lookupKey);
            Assert.True(row.CanView);
            Assert.False(row.CanCreate);
            Assert.False(row.CanEdit);
            Assert.False(row.CanDelete);
        }
    }

    [Fact]
    public async Task EnsureCreated_PremiseManagerRole_HasPremiseViewAndCorrectiveAction()
    {
        var db = await CreateSeededDatabaseAsync();

        var rows = await db.RolePermissions
            .Where(row => row.RoleId == RoleSeedData.PremiseManagerRoleId)
            .ToListAsync();

        Assert.Equal(4, rows.Count);

        var premise = Assert.Single(rows, row => row.PermissionKey == PermissionKeys.PremiseManagePremise);
        Assert.True(premise.CanView);
        Assert.False(premise.CanCreate);
        Assert.False(premise.CanEdit);
        Assert.False(premise.CanDelete);

        // Fills corrective actions on a non conformance (D-19): read it and edit it, nothing else.
        var nonConformance = Assert.Single(rows, row => row.PermissionKey == PermissionKeys.AuditNonConformance);
        Assert.True(nonConformance.CanView);
        Assert.False(nonConformance.CanCreate);
        Assert.True(nonConformance.CanEdit);
        Assert.False(nonConformance.CanDelete);

        Assert.DoesNotContain(rows, row => row.PermissionKey == PermissionKeys.AdminUsers);
    }

    [Fact]
    public async Task EnsureCreated_PermissionRows_OnlyUseKnownKeysAndAreUniquePerRole()
    {
        var db = await CreateSeededDatabaseAsync();

        var rows = await db.RolePermissions.ToListAsync();

        Assert.All(rows, row => Assert.Contains(row.PermissionKey, PermissionKeys.All));
        Assert.Equal(rows.Count, rows.Select(row => (row.RoleId, row.PermissionKey)).Distinct().Count());
        Assert.All(rows, row => Assert.Equal("system", row.SysUserCreated));
    }

    [Fact]
    public void All_ContainsNoDuplicates()
    {
        Assert.Equal(PermissionKeys.All.Count, PermissionKeys.All.Distinct(StringComparer.Ordinal).Count());
        Assert.All(PermissionKeys.All, key => Assert.False(string.IsNullOrWhiteSpace(key)));
    }
}

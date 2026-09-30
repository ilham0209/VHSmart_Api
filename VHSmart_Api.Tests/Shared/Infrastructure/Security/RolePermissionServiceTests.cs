using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Security;

public class RolePermissionServiceTests
{
    private static async Task<TestableVHSmartDbContext> CreateSeededDatabaseAsync()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public async Task HasPermissionAsync_GrantedRow_IsAllowedOnlyForTheGrantedActions()
    {
        var db = await CreateSeededDatabaseAsync();
        var service = new RolePermissionService(db, NewCache());

        Assert.True(await service.HasPermissionAsync(
            RoleSeedData.AuditorRoleId, PermissionKeys.AuditAuditTask, PermissionAction.View));
        Assert.True(await service.HasPermissionAsync(
            RoleSeedData.AuditorRoleId, PermissionKeys.AuditAuditTask, PermissionAction.Edit));

        // The same screen, an action the row does not grant: denied (default deny, D-19).
        Assert.False(await service.HasPermissionAsync(
            RoleSeedData.AuditorRoleId, PermissionKeys.AdminGeneralData, PermissionAction.Create));
    }

    [Fact]
    public async Task HasPermissionAsync_RowForAnotherScreen_IsDenied()
    {
        var db = await CreateSeededDatabaseAsync();
        var service = new RolePermissionService(db, NewCache());

        Assert.False(await service.HasPermissionAsync(
            RoleSeedData.AuditorRoleId, PermissionKeys.AdminUsers, PermissionAction.View));
    }

    [Fact]
    public async Task HasPermissionAsync_EmptyRoleIdOrKey_IsDenied()
    {
        var db = await CreateSeededDatabaseAsync();
        var service = new RolePermissionService(db, NewCache());

        Assert.False(await service.HasPermissionAsync(
            Guid.Empty, PermissionKeys.AdminUsers, PermissionAction.View));
        Assert.False(await service.HasPermissionAsync(
            RoleSeedData.VhSmartAdminRoleId, " ", PermissionAction.View));
        Assert.False(await service.HasPermissionAsync(
            RoleSeedData.VhSmartAdminRoleId, string.Empty, PermissionAction.View));
    }

    [Fact]
    public async Task HasPermissionAsync_SoftDeletedRow_IsDenied()
    {
        var db = await CreateSeededDatabaseAsync();
        var service = new RolePermissionService(db, NewCache());
        var row = await db.RolePermissions.FirstAsync(
            candidate => candidate.RoleId == RoleSeedData.VhSmartAdminRoleId
                && candidate.PermissionKey == PermissionKeys.AdminUsers);

        db.RolePermissions.Remove(row);
        await db.SaveChangesAsync();

        Assert.False(await service.HasPermissionAsync(
            RoleSeedData.VhSmartAdminRoleId, PermissionKeys.AdminUsers, PermissionAction.View));
    }

    [Fact]
    public async Task HasPermissionAsync_CachesRows_UntilInvalidateIsCalled()
    {
        var db = await CreateSeededDatabaseAsync();
        using var cache = NewCache();
        var service = new RolePermissionService(db, cache);
        var role = RoleSeedData.PremiseManagerRoleId;

        Assert.True(await service.HasPermissionAsync(
            role, PermissionKeys.PremiseManagePremise, PermissionAction.View));

        var row = await db.RolePermissions.FirstAsync(
            candidate => candidate.RoleId == role
                && candidate.PermissionKey == PermissionKeys.PremiseManagePremise);
        db.RolePermissions.Remove(row);
        await db.SaveChangesAsync();

        // Still granted: the answer came from the cache, not from the table.
        Assert.True(await service.HasPermissionAsync(
            role, PermissionKeys.PremiseManagePremise, PermissionAction.View));

        service.Invalidate(role);

        Assert.False(await service.HasPermissionAsync(
            role, PermissionKeys.PremiseManagePremise, PermissionAction.View));
    }

    [Fact]
    public async Task Invalidate_UnknownRole_DoesNotThrow()
    {
        var db = await CreateSeededDatabaseAsync();
        var service = new RolePermissionService(db, NewCache());

        service.Invalidate(Guid.NewGuid());

        Assert.False(await service.HasPermissionAsync(
            Guid.NewGuid(), PermissionKeys.AdminUsers, PermissionAction.View));
    }

    [Fact]
    public void ServiceProvider_ResolvesRolePermissionService()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var scope = factory.Services.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IPermissionService>();

        Assert.IsType<RolePermissionService>(service);
    }

    private static MemoryCache NewCache() => new(new MemoryCacheOptions());
}

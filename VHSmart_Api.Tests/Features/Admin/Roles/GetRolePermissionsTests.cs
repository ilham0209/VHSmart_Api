using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.Roles;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Admin.Roles;

public class GetRolePermissionsTests
{
    private static TestCurrentUser PlatformAdmin() =>
        new(Guid.NewGuid().ToString(), Guid.NewGuid(), isPlatformAdmin: true);

    private static async Task<TestableVHSmartDbContext> CreateSeededDatabaseAsync()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), PlatformAdmin());
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public async Task Handle_VhSmartAdmin_ReturnsEveryScreenKeyAsGranted()
    {
        var db = await CreateSeededDatabaseAsync();

        var result = await new GetRolePermissionsHandler(db, PlatformAdmin())
            .Handle(new GetRolePermissionsQuery(RoleSeedData.VhSmartAdminRoleId), CancellationToken.None);

        Assert.Equal(RoleSeedData.VhSmartAdminRoleId, result.RoleId);
        Assert.Equal("VH Smart Admin", result.Name);
        Assert.Equal(PermissionKeys.All.Count, result.Permissions.Count);
        Assert.All(result.Permissions, permission =>
        {
            Assert.True(permission.CanView);
            Assert.True(permission.CanCreate);
            Assert.True(permission.CanEdit);
            Assert.True(permission.CanDelete);
        });
    }

    [Fact]
    public async Task Handle_UngrantedScreen_IsReturnedDenied()
    {
        var db = await CreateSeededDatabaseAsync();

        var result = await new GetRolePermissionsHandler(db, PlatformAdmin())
            .Handle(new GetRolePermissionsQuery(RoleSeedData.AuditorRoleId), CancellationToken.None);

        // Every key is listed even when no row exists - denied is what the screen must show.
        Assert.Equal(PermissionKeys.All.Count, result.Permissions.Count);

        var payment = Assert.Single(
            result.Permissions, permission => permission.PermissionKey == PermissionKeys.PaymentCertificate);
        Assert.False(payment.CanView);
        Assert.False(payment.CanCreate);
        Assert.False(payment.CanEdit);
        Assert.False(payment.CanDelete);

        var auditTask = Assert.Single(
            result.Permissions, permission => permission.PermissionKey == PermissionKeys.AuditAuditTask);
        Assert.True(auditTask.CanView);
    }

    [Fact]
    public async Task Handle_UnknownRole_ThrowsNotFound()
    {
        var db = await CreateSeededDatabaseAsync();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => new GetRolePermissionsHandler(db, PlatformAdmin())
                .Handle(new GetRolePermissionsQuery(Guid.NewGuid()), CancellationToken.None));

        Assert.Equal("Role not found.", exception.Message);
    }

    [Fact]
    public async Task Handle_SoftDeletedRole_ThrowsNotFound()
    {
        var db = await CreateSeededDatabaseAsync();
        var role = await db.Roles.FirstAsync(candidate => candidate.Id == RoleSeedData.AuditorRoleId);
        db.Roles.Remove(role);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(
            () => new GetRolePermissionsHandler(db, PlatformAdmin())
                .Handle(new GetRolePermissionsQuery(RoleSeedData.AuditorRoleId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NotPlatformAdmin_ThrowsForbidden()
    {
        var db = await CreateSeededDatabaseAsync();
        var companyUser = new TestCurrentUser(Guid.NewGuid().ToString(), Guid.NewGuid());

        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => new GetRolePermissionsHandler(db, companyUser)
                .Handle(new GetRolePermissionsQuery(RoleSeedData.VhSmartAdminRoleId), CancellationToken.None));

        Assert.Equal("Only a platform administrator can manage roles.", exception.Message);
    }
}

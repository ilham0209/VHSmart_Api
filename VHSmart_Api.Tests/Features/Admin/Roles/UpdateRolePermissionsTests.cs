using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VHSmart_Api.Features.Admin.Roles;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Admin.Roles;

public class UpdateRolePermissionsTests
{
    private static TestCurrentUser PlatformAdmin() =>
        new(Guid.NewGuid().ToString(), Guid.NewGuid(), isPlatformAdmin: true);

    private static async Task<TestableVHSmartDbContext> CreateSeededDatabaseAsync(ICurrentUser? user = null)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user ?? PlatformAdmin());
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static RolePermissionInput Grant(string key, bool view = true, bool create = false, bool edit = false, bool delete = false) =>
        new(key, view, create, edit, delete);

    [Fact]
    public async Task Handle_ValidAssignment_ReplacesWholeMatrix()
    {
        var db = await CreateSeededDatabaseAsync();
        var handler = new UpdateRolePermissionsHandler(db, PlatformAdmin(), new NoCachePermissionService());

        var result = await handler.Handle(
            new UpdateRolePermissionsCommand(
                RoleSeedData.VhSmartAdminRoleId,
                [Grant(PermissionKeys.AdminUsers, view: true, create: true, edit: true)]),
            CancellationToken.None);

        // Only the assigned screen is live; the seeded rows are revoked with a soft delete.
        var live = await db.RolePermissions
            .Where(row => row.RoleId == RoleSeedData.VhSmartAdminRoleId)
            .ToListAsync();
        var single = Assert.Single(live);
        Assert.Equal(PermissionKeys.AdminUsers, single.PermissionKey);
        Assert.True(single.CanView);
        Assert.True(single.CanCreate);
        Assert.True(single.CanEdit);
        // The seeded row granted Delete; the assignment did not, so it was turned off.
        Assert.False(single.CanDelete);

        var deleted = await db.RolePermissions
            .IgnoreQueryFilters()
            .Where(row => row.RoleId == RoleSeedData.VhSmartAdminRoleId && row.IsDeleted)
            .CountAsync();
        Assert.Equal(PermissionKeys.All.Count - 1, deleted);

        // The answer is the full matrix again, so the screen can repaint without a second call.
        Assert.Equal(PermissionKeys.All.Count, result.Permissions.Count);
        var assigned = Assert.Single(
            result.Permissions, permission => permission.PermissionKey == PermissionKeys.AdminUsers);
        Assert.True(assigned.CanCreate);
        Assert.False(result.Permissions.First(
            permission => permission.PermissionKey == PermissionKeys.PaymentCertificate).CanView);
    }

    [Fact]
    public async Task Handle_ExistingAndNewScreens_UpdatesAndAddsRows()
    {
        var db = await CreateSeededDatabaseAsync();
        var handler = new UpdateRolePermissionsHandler(db, PlatformAdmin(), new NoCachePermissionService());
        var auditorRoleId = Guid.NewGuid();
        db.Roles.Add(new RoleEntity { Name = "Custom", IsSystemRole = false, Id = auditorRoleId });
        await db.SaveChangesAsync();

        await handler.Handle(
            new UpdateRolePermissionsCommand(
                auditorRoleId,
                [
                    Grant(PermissionKeys.AuditRecommendation, view: true, edit: true),
                    Grant(PermissionKeys.Dashboard)
                ]),
            CancellationToken.None);

        await handler.Handle(
            new UpdateRolePermissionsCommand(
                auditorRoleId,
                [
                    Grant(PermissionKeys.AuditRecommendation, view: true, create: true, delete: true),
                    Grant(PermissionKeys.SupportSubmit, view: true)
                ]),
            CancellationToken.None);

        var rows = await db.RolePermissions
            .Where(row => row.RoleId == auditorRoleId)
            .OrderBy(row => row.PermissionKey)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        var recommendation = Assert.Single(rows, row => row.PermissionKey == PermissionKeys.AuditRecommendation);
        Assert.True(recommendation.CanView);
        Assert.True(recommendation.CanCreate);
        Assert.False(recommendation.CanEdit);
        Assert.True(recommendation.CanDelete);
        Assert.Contains(rows, row => row.PermissionKey == PermissionKeys.SupportSubmit);
        Assert.DoesNotContain(rows, row => row.PermissionKey == PermissionKeys.Dashboard);
    }

    [Fact]
    public async Task Handle_AllFlagsFalse_StoresNoRow()
    {
        var db = await CreateSeededDatabaseAsync();
        var handler = new UpdateRolePermissionsHandler(db, PlatformAdmin(), new NoCachePermissionService());

        await handler.Handle(
            new UpdateRolePermissionsCommand(
                RoleSeedData.AuditorRoleId,
                [Grant(PermissionKeys.AdminUsers, view: false)]),
            CancellationToken.None);

        var live = await db.RolePermissions
            .Where(row => row.RoleId == RoleSeedData.AuditorRoleId)
            .ToListAsync();
        Assert.DoesNotContain(live, row => row.PermissionKey == PermissionKeys.AdminUsers);
        Assert.DoesNotContain(live, row => row.PermissionKey == PermissionKeys.AuditAuditTask);
    }

    [Fact]
    public async Task Handle_EmptyPermissionList_RevokesEverything()
    {
        var db = await CreateSeededDatabaseAsync();
        var handler = new UpdateRolePermissionsHandler(db, PlatformAdmin(), new NoCachePermissionService());

        var result = await handler.Handle(
            new UpdateRolePermissionsCommand(RoleSeedData.AuditorRoleId, []),
            CancellationToken.None);

        var live = await db.RolePermissions
            .Where(row => row.RoleId == RoleSeedData.AuditorRoleId)
            .ToListAsync();
        Assert.Empty(live);
        Assert.Equal(PermissionKeys.All.Count, result.Permissions.Count);
        Assert.All(result.Permissions, permission => Assert.False(permission.CanView));
    }

    [Fact]
    public async Task Handle_Assignment_InvalidatesCachedPermissions()
    {
        var db = await CreateSeededDatabaseAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var permissionService = new RolePermissionService(db, cache);
        var role = RoleSeedData.PremiseManagerRoleId;

        var before = await permissionService.HasPermissionAsync(
            role, PermissionKeys.PremiseManagePremise, PermissionAction.View);
        Assert.True(before);

        var handler = new UpdateRolePermissionsHandler(db, PlatformAdmin(), permissionService);
        await handler.Handle(
            new UpdateRolePermissionsCommand(
                role, [Grant(PermissionKeys.AuditFinding, view: true)]),
            CancellationToken.None);

        // Without Invalidate the answer would still come from the cached seed rows.
        var afterPremise = await permissionService.HasPermissionAsync(
            role, PermissionKeys.PremiseManagePremise, PermissionAction.View);
        var afterFinding = await permissionService.HasPermissionAsync(
            role, PermissionKeys.AuditFinding, PermissionAction.View);

        Assert.False(afterPremise);
        Assert.True(afterFinding);
    }

    [Fact]
    public async Task Handle_NotPlatformAdmin_ThrowsForbidden()
    {
        var db = await CreateSeededDatabaseAsync();
        var rowsBefore = await db.RolePermissions.IgnoreQueryFilters().CountAsync();
        var companyUser = new TestCurrentUser(Guid.NewGuid().ToString(), Guid.NewGuid());
        var handler = new UpdateRolePermissionsHandler(db, companyUser, new NoCachePermissionService());

        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(
                new UpdateRolePermissionsCommand(
                    RoleSeedData.VhSmartAdminRoleId, [Grant(PermissionKeys.AdminUsers)]),
                CancellationToken.None));

        Assert.Equal("Only a platform administrator can manage roles.", exception.Message);

        // Nothing was written: a company user cannot touch the matrix at all.
        var rowsAfter = await db.RolePermissions.IgnoreQueryFilters().CountAsync();
        var revoked = await db.RolePermissions.IgnoreQueryFilters().CountAsync(row => row.IsDeleted);
        Assert.Equal(rowsBefore, rowsAfter);
        Assert.Equal(0, revoked);
    }

    [Fact]
    public async Task Handle_UnknownRole_ThrowsNotFound()
    {
        var db = await CreateSeededDatabaseAsync();
        var handler = new UpdateRolePermissionsHandler(db, PlatformAdmin(), new NoCachePermissionService());

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(
                new UpdateRolePermissionsCommand(Guid.NewGuid(), [Grant(PermissionKeys.AdminUsers)]),
                CancellationToken.None));

        Assert.Equal("Role not found.", exception.Message);
    }

    [Fact]
    public void Validate_EmptyRoleId_IsInvalid()
    {
        var result = new UpdateRolePermissionsValidator().Validate(
            new UpdateRolePermissionsCommand(Guid.Empty, [Grant(PermissionKeys.AdminUsers)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateRolePermissionsCommand.RoleId));
    }

    [Fact]
    public void Validate_NullPermissions_IsInvalid()
    {
        var result = new UpdateRolePermissionsValidator().Validate(
            new UpdateRolePermissionsCommand(RoleSeedData.VhSmartAdminRoleId, null!));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateRolePermissionsCommand.Permissions));
    }

    [Fact]
    public void Validate_UnknownPermissionKey_IsInvalid()
    {
        var result = new UpdateRolePermissionsValidator().Validate(
            new UpdateRolePermissionsCommand(
                RoleSeedData.VhSmartAdminRoleId, [Grant("Admin.NotAScreen")]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.ErrorMessage == "Unknown permission key 'Admin.NotAScreen'.");
    }

    [Fact]
    public void Validate_EmptyPermissionKey_IsInvalid()
    {
        var result = new UpdateRolePermissionsValidator().Validate(
            new UpdateRolePermissionsCommand(RoleSeedData.VhSmartAdminRoleId, [Grant(" ")]));

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void Validate_PermissionKeyLongerThan100Characters_IsInvalid()
    {
        var result = new UpdateRolePermissionsValidator().Validate(
            new UpdateRolePermissionsCommand(
                RoleSeedData.VhSmartAdminRoleId, [Grant(new string('k', 101))]));

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void Validate_DuplicatePermissionKey_IsInvalid()
    {
        var result = new UpdateRolePermissionsValidator().Validate(
            new UpdateRolePermissionsCommand(
                RoleSeedData.VhSmartAdminRoleId,
                [Grant(PermissionKeys.AdminUsers), Grant(PermissionKeys.AdminUsers, view: false)]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.ErrorMessage == "Each permission key may only be assigned once.");
    }

    [Fact]
    public void Validate_ValidAssignment_IsValid()
    {
        var result = new UpdateRolePermissionsValidator().Validate(
            new UpdateRolePermissionsCommand(
                RoleSeedData.VhSmartAdminRoleId,
                [Grant(PermissionKeys.AdminUsers, view: true, create: true)]));

        Assert.True(result.IsValid);
    }

    // A permission service that never caches, so the handler tests observe the database directly.
    private sealed class NoCachePermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(
            Guid roleId,
            string key,
            PermissionAction action,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public void Invalidate(Guid roleId)
        {
        }
    }
}

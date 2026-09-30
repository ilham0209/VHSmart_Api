using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Shared.Infrastructure.Security;

// Reads the role's AdmRolePermissions rows behind [HasPermission] (CodingRules 8.2, D-19).
// Fail closed: no roleId, no row, an unknown key or an unknown action is a denial - the legacy
// "permissions default to allow" defect must not come back (spec 23). The matrix is cached per
// role for a short time and dropped explicitly when the role is edited, so a change is visible
// on the next request without a restart.
public sealed class RolePermissionService(VHSmartDbContext db, IMemoryCache cache)
    : IPermissionService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private sealed record RoleGrant(
        string Key,
        bool CanView,
        bool CanCreate,
        bool CanEdit,
        bool CanDelete);

    public async Task<bool> HasPermissionAsync(
        Guid roleId,
        string key,
        PermissionAction action,
        CancellationToken cancellationToken = default)
    {
        if (roleId == Guid.Empty || string.IsNullOrWhiteSpace(key))
            return false;

        var grants = await GrantsAsync(roleId, cancellationToken);
        var grant = grants.FirstOrDefault(row => string.Equals(row.Key, key, StringComparison.Ordinal));

        if (grant is null)
            return false;

        return action switch
        {
            PermissionAction.View => grant.CanView,
            PermissionAction.Create => grant.CanCreate,
            PermissionAction.Edit => grant.CanEdit,
            PermissionAction.Delete => grant.CanDelete,
            // A value outside the enum (a caller passing a cast int) is denied, never granted.
            _ => false
        };
    }

    public void Invalidate(Guid roleId) => cache.Remove(CacheKey(roleId));

    private async Task<IReadOnlyList<RoleGrant>> GrantsAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var grants = await cache.GetOrCreateAsync(
            CacheKey(roleId),
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;

                return await db.RolePermissions
                    .AsNoTracking()
                    .Where(row => row.RoleId == roleId)
                    .Select(row => new RoleGrant(
                        row.PermissionKey, row.CanView, row.CanCreate, row.CanEdit, row.CanDelete))
                    .ToListAsync(cancellationToken);
            });

        return grants ?? [];
    }

    private static string CacheKey(Guid roleId) => $"role-permissions:{roleId:N}";
}

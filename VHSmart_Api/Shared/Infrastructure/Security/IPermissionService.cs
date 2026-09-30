namespace VHSmart_Api.Shared.Infrastructure.Security;

// Role -> permission lookup behind [HasPermission] (CodingRules 8.2). Implementations must fail
// closed: a missing AdmRolePermissions row, an unknown key or an unauthenticated caller is a
// denial, never a grant (legacy defect "permissions default to allow", spec 23).
public interface IPermissionService
{
    Task<bool> HasPermissionAsync(
        Guid roleId,
        string key,
        PermissionAction action,
        CancellationToken cancellationToken = default);

    // The matrix is cached per role (CodingRules 8.2), so saving a role's permissions has to
    // drop that entry - the next check then re-reads AdmRolePermissions. No-op for implementations
    // that do not cache.
    void Invalidate(Guid roleId);
}

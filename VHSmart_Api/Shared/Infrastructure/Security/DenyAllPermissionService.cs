namespace VHSmart_Api.Shared.Infrastructure.Security;

// Default deny (CodingRules 8, D-19). AdmRolePermissions does not exist yet - it arrives with
// A-01 - so there is no permission data to read and nothing is ever granted. Replaced there by
// the cached, per-role implementation.
public sealed class DenyAllPermissionService : IPermissionService
{
    public Task<bool> HasPermissionAsync(
        Guid roleId,
        string key,
        PermissionAction action,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}

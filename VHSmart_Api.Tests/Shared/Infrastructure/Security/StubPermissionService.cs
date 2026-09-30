using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Security;

// Stands in for AdmRolePermissions (A-01) so a granted and a denied role can be asserted without
// the table existing yet. Anything not listed is denied, exactly like the real service.
internal sealed class StubPermissionService : IPermissionService
{
    private readonly HashSet<(Guid RoleId, string Key, PermissionAction Action)> _granted;

    public StubPermissionService(IEnumerable<(Guid RoleId, string Key, PermissionAction Action)> granted) =>
        _granted = [.. granted];

    public Task<bool> HasPermissionAsync(
        Guid roleId,
        string key,
        PermissionAction action,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_granted.Contains((roleId, key, action)));
}

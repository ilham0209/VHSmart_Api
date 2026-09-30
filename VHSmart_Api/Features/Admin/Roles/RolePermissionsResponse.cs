using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.Roles;

// One row of the permission matrix screen: a screen key plus its four action flags.
public record RolePermissionResponse(
    string PermissionKey,
    bool CanView,
    bool CanCreate,
    bool CanEdit,
    bool CanDelete);

// The whole matrix of one role. Every screen key is always present: a key with no stored row is
// denied (default deny), and the client has to show those denied rows too (CodingRules 8.2).
public record RolePermissionsResponse(
    Guid RoleId,
    string Name,
    IReadOnlyList<RolePermissionResponse> Permissions);

internal static class RolePermissionMatrix
{
    public static IReadOnlyList<RolePermissionResponse> Build(IEnumerable<RolePermissionEntity> rows)
    {
        var stored = rows.ToDictionary(row => row.PermissionKey, StringComparer.Ordinal);

        return PermissionKeys.All
            .Select(key => stored.TryGetValue(key, out var row)
                ? new RolePermissionResponse(key, row.CanView, row.CanCreate, row.CanEdit, row.CanDelete)
                : new RolePermissionResponse(key, false, false, false, false))
            .ToArray();
    }
}

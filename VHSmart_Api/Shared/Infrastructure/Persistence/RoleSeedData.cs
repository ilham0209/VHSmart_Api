using System.Security.Cryptography;
using System.Text;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Shared.Infrastructure.Persistence;

// The default permission matrix of the 3 system roles (D-19, spec 3.1). Q8 is still open, so
// these are only defaults: the rows land in AdmRoles / AdmRolePermissions as ordinary data and
// the Roles API may change them later - a re-seed never overwrites the owner's edits (HasData
// runs once, inside the migration).
//
// HasData needs a stable primary key on every machine, so each id is a hash of a fixed string
// instead of Guid.NewGuid().
public static class RoleSeedData
{
    public static readonly Guid VhSmartAdminRoleId = StableId("role:vh-smart-admin");

    public static readonly Guid AuditorRoleId = StableId("role:auditor-chief-auditor");

    public static readonly Guid PremiseManagerRoleId = StableId("role:restaurant-premise-manager");

    private const string SeedUser = "system";

    private static readonly DateTime SeedDate = new(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

    public sealed record PermissionGrant(
        string Key,
        bool CanView,
        bool CanCreate,
        bool CanEdit,
        bool CanDelete);

    public sealed record RoleSeed(
        Guid Id,
        string Name,
        bool IsSystemRole,
        IReadOnlyList<PermissionGrant> Permissions);

    // Declared before Seeds: static field initializers run in textual order.
    private static readonly IReadOnlyList<string> AuditKeys = PermissionKeys.All
        .Where(key => key.StartsWith("Audit.", StringComparison.Ordinal))
        .ToArray();

    public static readonly IReadOnlyList<RoleSeed> Seeds =
    [
        // Company super admin: every screen, every action (D-19 "all permissions").
        new(VhSmartAdminRoleId, "VH Smart Admin", true, GrantAll(PermissionKeys.All)),

        // Audit work plus the data the audit screens read: reference data (spec 14.1 lives in
        // General Data) and the premise pick-list of Audit Planning (spec 14.8). Dashboard and
        // Account Setting are the personal screens every signed-in role needs.
        new(AuditorRoleId, "Auditor / Chief Auditor", true,
        [
            .. GrantAll(AuditKeys),
            GrantView(PermissionKeys.Dashboard),
            GrantView(PermissionKeys.AccountSetting),
            GrantView(PermissionKeys.AdminGeneralData),
            GrantView(PermissionKeys.PremiseManagePremise)
        ]),

        // Own premise data (read) + fills corrective actions (View/Edit) per D-19.
        new(PremiseManagerRoleId, "Restaurant / Premise Manager", true,
        [
            GrantView(PermissionKeys.Dashboard),
            GrantView(PermissionKeys.AccountSetting),
            GrantView(PermissionKeys.PremiseManagePremise),
            new PermissionGrant(PermissionKeys.AuditNonConformance,
                CanView: true, CanCreate: false, CanEdit: true, CanDelete: false)
        ])
    ];

    public static IEnumerable<RoleEntity> RoleEntities() =>
        Seeds.Select(seed => new RoleEntity
        {
            Id = seed.Id,
            Name = seed.Name,
            IsSystemRole = seed.IsSystemRole,
            SysUserCreated = SeedUser,
            SysDateCreated = SeedDate
        });

    public static IEnumerable<RolePermissionEntity> PermissionEntities() =>
        Seeds.SelectMany(seed => seed.Permissions.Select(grant => new RolePermissionEntity
        {
            Id = PermissionId(seed.Id, grant.Key),
            RoleId = seed.Id,
            PermissionKey = grant.Key,
            CanView = grant.CanView,
            CanCreate = grant.CanCreate,
            CanEdit = grant.CanEdit,
            CanDelete = grant.CanDelete,
            SysUserCreated = SeedUser,
            SysDateCreated = SeedDate
        }));

    public static Guid PermissionId(Guid roleId, string permissionKey) =>
        StableId($"role-permission:{roleId:N}:{permissionKey}");

    private static IReadOnlyList<PermissionGrant> GrantAll(IEnumerable<string> keys) =>
        keys.Select(key => new PermissionGrant(key, true, true, true, true)).ToArray();

    private static PermissionGrant GrantView(string key) =>
        new(key, true, false, false, false);

    private static Guid StableId(string value) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);
}

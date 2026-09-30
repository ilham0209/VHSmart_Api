namespace VHSmart_Api.Shared.Domain.Admin;

// A role is the holder of the permission matrix (D-19): users get a role (AdmUsers.RoleId) and
// the role holds AdmRolePermissions rows. Seeded rows are system roles with CompanyId null; a
// company-specific role would set CompanyId. The table is NOT an ITenantEntity - Database.md
// states the tenant filter does not apply here, so a system role stays visible to every company.
public class RoleEntity : BaseClass
{
    public Guid? CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsSystemRole { get; set; }
}

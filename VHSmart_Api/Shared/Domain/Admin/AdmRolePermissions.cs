namespace VHSmart_Api.Shared.Domain.Admin;

// One screen's four action flags for one role, UQ (RoleId, PermissionKey) (Database.md).
// A missing row - or a row with every flag false - means every action on that screen is denied:
// permissions are default deny (CodingRules 8.2, D-19).
public class RolePermissionEntity : BaseClass
{
    public Guid RoleId { get; set; }

    public string PermissionKey { get; set; } = string.Empty;

    public bool CanView { get; set; }

    public bool CanCreate { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDelete { get; set; }
}

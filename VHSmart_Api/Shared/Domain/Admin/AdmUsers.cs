namespace VHSmart_Api.Shared.Domain.Admin;

// A login identity that can belong to several companies (spec 3.1-3.2, Database.md 5).
// [G] global table: deliberately not an ITenantEntity - the row itself is not owned by a
// company, the memberships in AdmUserCompanies are. Name is stored in upper case and
// PasswordHash never appears in any response (CodingRules 8.3).
public class UserEntity : BaseClass
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool MustChangePassword { get; set; }

    public bool IsActive { get; set; }

    public Guid RoleId { get; set; }

    // Serunai Super User: sees all companies (D-07, spec 3.1).
    public bool IsPlatformAdmin { get; set; }

    public string? ContactNo { get; set; }

    public StoredFile? ProfilePicture { get; set; }

    public DateTime? ActivatedAt { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTime? LockedUntil { get; set; }

    public DateTime? LastLoginAt { get; set; }

    // No FK yet: ComStaff arrives with P-01 (Database.md 5).
    public Guid? StaffId { get; set; }
}

namespace VHSmart_Api.Shared.Domain.Admin;

// The companies one user may sign in to (Database.md 5; "Please add List of Company",
// spec 3.4). Not an ITenantEntity on purpose: login runs before an active company exists,
// and Switch Company must read the whole membership list of the caller (spec 7.4, D-29).
public class UserCompanyEntity : BaseClass
{
    public Guid UserId { get; set; }

    public Guid CompanyId { get; set; }

    // The row login picks when no company was chosen yet.
    public bool IsDefault { get; set; }
}

namespace VHSmart_Api.Shared.Domain.Admin;

// The 6 saleable packages of spec 21.5 (Database.md 5): codes only, plus the limits
// Database.md records for them. Enforcement is out of MVP (D-12) - the rows are stored data,
// nothing checks MaxUsers / MaxPremises yet. [G] global table: one list for every company.
public class SubscriptionPackageEntity : BaseClass
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int? MaxUsers { get; set; }

    public int? MaxPremises { get; set; }
}

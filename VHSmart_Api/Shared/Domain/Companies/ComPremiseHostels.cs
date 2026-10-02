namespace VHSmart_Api.Shared.Domain.Companies;

// Premise > Manage Premise (spec 7.7 tab "Facility Information"): the "Hostel Information"
// table. HostelName is required (Database.md 7 marker), the rest optional; legacy adds,
// views and deletes hostels from this tab ([CODE] - spec 7.7 shows the table only).
public class PremiseHostelEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid PremiseId { get; set; }

    public PremiseEntity? Premise { get; set; }

    public string HostelName { get; set; } = string.Empty;

    public string? Address { get; set; }

    public DateTime? TenancyExpiryDate { get; set; }

    public string? ContactPerson { get; set; }

    public string? PhoneNo { get; set; }
}

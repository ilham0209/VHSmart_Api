namespace VHSmart_Api.Shared.Domain.Companies;

// Premise > Manage Premise (spec 7.7 tab "Staff Information"): the "Contact Person" table,
// rows chosen from All Staff. UQ (PremiseId, StaffId) among live rows (Database.md 7).
public class PremiseContactEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid PremiseId { get; set; }

    public PremiseEntity? Premise { get; set; }

    public Guid StaffId { get; set; }

    public StaffEntity? Staff { get; set; }

    // Resolved through Staff.Designation in the response; kept out of the entity on purpose.
}

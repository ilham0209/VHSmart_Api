namespace VHSmart_Api.Shared.Domain.Audit;

// The Finding screen (spec 14.4 [CONFIRMED]): an audit statement per company, linked to at
// least one Recommendation (the Audit chain: Recommendation -> Finding -> Criteria, spec
// 14.0). Audit setup data is per company (spec 14.0 [CONFIRMED]) so the row is [T]; UQ
// (CompanyId, FindingCode) among live rows (Database.md 11). Spec 21.9 also claims the
// description is unique, but Database.md carries no such index - not enforced, flagged.
public class FindingEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string FindingCode { get; set; } = string.Empty;

    public string? Description { get; set; }
}

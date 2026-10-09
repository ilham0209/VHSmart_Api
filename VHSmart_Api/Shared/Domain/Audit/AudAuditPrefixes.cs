namespace VHSmart_Api.Shared.Domain.Audit;

// The Audit Prefix screen (spec 14.2 [CONFIRMED]): a short code per Brand, used later by
// Audit Planning to build the Audit Reference No. (D-09 - "Prefix comes from the premise's
// Brand"). Audit setup data is per company: each company's super admin maintains their own
// (spec 14.0, [CONFIRMED]), so the row is [T] and UQ (CompanyId, BrandId) among live rows
// (Database.md 11 - "VERIFY, default enforce" - one prefix slot per brand).
public class AuditPrefixEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid BrandId { get; set; }

    public string Prefix { get; set; } = string.Empty;

    public string? Description { get; set; }
}

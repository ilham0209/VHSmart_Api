namespace VHSmart_Api.Shared.Domain.Audit;

// "List of Premise" of an external audit report (spec 14.10, Database.md 13): which of
// the audited company's premises the report covers. UQ among live rows so a re-save of
// the premise picker never duplicates a pair.
public class ExternalAuditReportPremiseEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid ExternalAuditReportId { get; set; }

    public Guid PremiseId { get; set; }
}

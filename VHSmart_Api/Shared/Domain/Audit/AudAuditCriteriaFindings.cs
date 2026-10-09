namespace VHSmart_Api.Shared.Domain.Audit;

// The "Finding Selection" dropdown of the criteria modal (spec 14.5 [MANUAL]: "A Criteria
// can be linked to Findings through Finding Selection (link is optional)"). [T] junction -
// Database.md 11 carries CompanyId even on child tables. No unique pair index: unlike
// AudFindingRecommendations, Database.md 11 states no "UQ pair" for this table, so the
// handler collapses duplicate ids instead (flagged in the AU-04 report).
public class AuditCriteriaFindingEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid AuditCriteriaId { get; set; }

    public Guid FindingId { get; set; }
}

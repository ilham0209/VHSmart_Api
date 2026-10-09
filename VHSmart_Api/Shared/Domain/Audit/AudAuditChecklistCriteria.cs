namespace VHSmart_Api.Shared.Domain.Audit;

// The Criteria Selection of a checklist (spec 14.6 modal, Database.md 12): which Audit
// Criteria rows the checklist contains, saved AFTER the header (the two-step flow of
// spec 14.6 [MANUAL]). UQ pair (Database.md 12) - applied filtered on the company and the
// live rows like every other junction, so an un-tick frees the pair for re-ticking.
public class AuditChecklistCriteriaEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid ChecklistId { get; set; }

    public Guid AuditCriteriaId { get; set; }
}

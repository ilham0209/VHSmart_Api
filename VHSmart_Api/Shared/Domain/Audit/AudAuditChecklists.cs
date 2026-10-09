namespace VHSmart_Api.Shared.Domain.Audit;

// One row of the "List Audit Checklist" list (spec 14.6, Database.md 12): a named set of
// Audit Criteria an auditor answers, called "template" in Audit Planning and Group Auditor.
// Checklist Category is a General Data value (the probable §14.1 audit-category mapping
// [VERIFY] - flagged). A checklist that is linked to any planned/performed audit cannot be
// edited or deleted (spec 14.6 [MANUAL], Database.md 12 "Locked once used by any
// AudAuditPlans row - computed"): the lock is checked in the handlers, not stored.
public class AuditChecklistEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid ChecklistCategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}

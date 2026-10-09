namespace VHSmart_Api.Shared.Domain.Audit;

// One planned audit (spec 14.8, Database.md 13). Modelled NOW because the lock rule of
// spec 14.6 ("a checklist linked to any transaction cannot be edited or deleted") is
// computed against this table - AudAuditChecklists has no other lock source (Database.md
// 12: "Locked once used by any AudAuditPlans row"). Full column set per Database.md 13 so
// the migration matches the documented schema exactly; no behaviour beyond the read the
// lock needs - Audit Planning itself is a later task (flagged in the AU-05 report).
// GroupAuditorId is a plain column: AudGroupAuditors is not part of the model yet, so its
// FK constraint is deferred to the Group Auditor task (flagged).
public class AuditPlanEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public string AuditReferenceNo { get; set; } = string.Empty;

    public Guid AuditPurposeId { get; set; }

    public Guid AuditTypeId { get; set; }

    public Guid CategoryId { get; set; }

    public Guid PremiseId { get; set; }

    public DateTime ScheduleDate { get; set; }

    public Guid GroupAuditorId { get; set; }

    public Guid ChecklistId { get; set; }

    public Guid AssignedByUserId { get; set; }

    public DateTime DateAssigned { get; set; }
}

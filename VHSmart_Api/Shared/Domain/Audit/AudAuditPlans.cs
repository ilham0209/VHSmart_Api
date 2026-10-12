namespace VHSmart_Api.Shared.Domain.Audit;

// One planned audit (spec 14.8, Database.md 13). Modelled in AU-05 because the lock rule
// of spec 14.6 ("a checklist linked to any transaction cannot be edited or deleted") is
// computed against this table; full column set per Database.md 13. The Audit Planning
// feature (AU-07) creates these rows (D-09 reference number, auto-created AuditTaskEntity
// companion). GroupAuditorId gained its FK with AU-06, when AudGroupAuditors joined the
// model.
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

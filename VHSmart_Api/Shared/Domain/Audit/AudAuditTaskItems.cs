namespace VHSmart_Api.Shared.Domain.Audit;

// One checklist row of an audit task (spec 14.9 tab "Audit Checklist", Database.md 13):
// a SNAPSHOT of the linked checklist's Audit Criteria at Start Audit, "so later edits never
// change past audits". Category/Criteria/SubCriteria/Reference/PotentialPoint are frozen
// text/points; ActualPoint + IsConforming + Remarks are the auditor's answers while the
// audit is InProgress; FinalPoint is the post-completion Final Review value (D-06: Chief
// Auditor only, after Completed - the seeded role model has no chief/auditor split, so the
// role gate is the Auditor/Chief Auditor role itself, flagged).
public class AuditTaskItemEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid AuditTaskId { get; set; }

    // The Audit Criteria row the snapshot came from - kept for traceability; the snapshot
    // columns stand alone if the source row changes later.
    public Guid? SourceAuditCriteriaId { get; set; }

    public string Category { get; set; } = string.Empty;

    public int CategorySequence { get; set; }

    public string Criteria { get; set; } = string.Empty;

    public int CriteriaSequence { get; set; }

    public string? SubCriteria { get; set; }

    public string? Reference { get; set; }

    public decimal PotentialPoint { get; set; }

    public decimal? ActualPoint { get; set; }

    public decimal? FinalPoint { get; set; }

    // null = unanswered (Database.md 13); how Conformance Yes/No is entered is [VERIFY]
    // in the spec, so the column is optional and never derived.
    public bool? IsConforming { get; set; }

    public string? Remarks { get; set; }
}

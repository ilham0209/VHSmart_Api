namespace VHSmart_Api.Shared.Domain.Audit;

// Status lifecycle of an audit task (spec 14.9 [CONFIRMED - owner], Database.md 13):
// NotStarted -> InProgress -> Completed (one way; Completed is locked). Transitions live
// in the Audit Task feature, not here.
public enum AuditTaskStatus
{
    NotStarted,
    InProgress,
    Completed
}

// One audit task, auto-created when its plan is saved (spec 14.8/14.9 flow step 1,
// Database.md 13): the 1:1 companion of AudAuditPlans (UQ AuditPlanId - Database.md 13).
// Created with Status = NotStarted and scores 0.00; Start Audit, the checklist snapshot,
// the Acknowledgement fields and the score writes belong to the Audit Task task (AU-08).
public class AuditTaskEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid AuditPlanId { get; set; }

    public AuditTaskStatus Status { get; set; } = AuditTaskStatus.NotStarted;

    public DateTime? StartAuditDate { get; set; }

    public TimeSpan? TimeIn { get; set; }

    public DateTime? EndAuditDate { get; set; }

    public TimeSpan? TimeOut { get; set; }

    public decimal AuditScore { get; set; }

    public decimal FinalReviewScore { get; set; }

    public string? Summary { get; set; }

    public string? AuditeeName { get; set; }

    public string? AuditeeDesignation { get; set; }

    public string? ReportingTo { get; set; }
}

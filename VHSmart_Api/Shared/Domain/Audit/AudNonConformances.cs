namespace VHSmart_Api.Shared.Domain.Audit;

// Who generated the NC (Database.md 13, spec 14.11 "Source of NCR"): internal audit
// items raise them at Answer/Submit time (AU-08 kept that for AU-10), the external
// report's NC tab raises the External ones (AU-09).
public enum NonConformanceSource
{
    Internal,
    External
}

// D-05 Non-Conformance lifecycle: Open -> CorrectiveActionSubmitted (auditee saves the
// corrective action) -> Closed (auditor sets the status, comment required). Reopen is
// not allowed. Text stored as string constants (D-05).
public static class NonConformanceStatus
{
    public const string Open = "Open";
    public const string CorrectiveActionSubmitted = "CorrectiveActionSubmitted";
    public const string Closed = "Closed";
}

// One non-conformance row (spec 14.10 NC tab / 14.11 list, Database.md 13). External
// rows are created on an external report's NC tab; internal rows will be created by the
// audit-task flow (AU-10 scope). ItemName/DetailName carry the picked premise/manufacturer
// and menu/product texts (the spec dropdowns); Findings is the internal-audit column and
// stays null on external rows (flagged). Tags meaning is unknown [VERIFY] - stored, never
// derived.
public class NonConformanceEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public NonConformanceSource Source { get; set; }

    public string? AuditReferenceNo { get; set; }

    public Guid? AuditTaskItemId { get; set; }

    public Guid? ExternalAuditReportId { get; set; }

    public Guid? PremiseId { get; set; }

    public string? ItemName { get; set; }

    public string? DetailName { get; set; }

    public string? Reference { get; set; }

    public string? Clause { get; set; }

    public string? Findings { get; set; }

    public string? NonConformanceDetails { get; set; }

    public Guid? CategoryId { get; set; }

    public string? Remarks { get; set; }

    public string? Tags { get; set; }

    public string? Status { get; set; } = NonConformanceStatus.Open;

    // Appears in the Final Non Conformance List (Database.md 13); false on creation.
    public bool IsFinal { get; set; }

    public DateTime? ClosedAt { get; set; }

    public Guid? ClosedBy { get; set; }
}

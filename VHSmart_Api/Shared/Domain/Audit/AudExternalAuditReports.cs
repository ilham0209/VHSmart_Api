namespace VHSmart_Api.Shared.Domain.Audit;

// Status of an external audit report (spec 14.12: "Opened -> ... [VERIFY]"). Only Opened
// is defined; the lifecycle beyond it is unknown, so create stamps it and no transition
// endpoint exists (flagged).
public static class ExternalAuditReportStatus
{
    public const string Opened = "Opened";
}

// One external audit report issued by a certification body (spec 14.10, Database.md 13):
// the header form of "Manage External Audit Report and Non Conformance". The audit
// reference number is nullable (Database.md 13 carries no *): the legacy sample
// "EXT-SUP/3/29-11-2025(3)" has no documented format and Decisions.md has no default,
// so it stays null until the owner defines the format (flagged). CompanyId is the
// audited company (identity from the JWT, CodingRules 8).
public class ExternalAuditReportEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public string? AuditReferenceNo { get; set; }

    public Guid ReceivedFromCertificationBodyId { get; set; }

    public string CbAuditReferenceNo { get; set; } = string.Empty;

    // Free text per Database.md 13 (100); the dropdown source is the D-10 General Data
    // pair, the value is stored as chosen.
    public string TypeOfApplication { get; set; } = string.Empty;

    public string TypeOfInspection { get; set; } = string.Empty;

    public Guid ApplicationSchemeId { get; set; }

    public string? CbHalalApplicationNo { get; set; }

    public string? CertificationNo { get; set; }

    public DateTime AuditDate { get; set; }

    public TimeSpan TimeIn { get; set; }

    public TimeSpan TimeOut { get; set; }

    public string? Remarks { get; set; }

    // D-10: fixed list Pass / Fail / Pending until the owner defines the values.
    public string? OverallStatus { get; set; }

    public string? Status { get; set; } = ExternalAuditReportStatus.Opened;
}

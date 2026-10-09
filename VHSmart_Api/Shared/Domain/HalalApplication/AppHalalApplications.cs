namespace VHSmart_Api.Shared.Domain.HalalApplication;

// The status strings of D-26, verbatim. New applications start at DRAFT; Submit and the
// "Tagging Application Status" screen move them through the rest (HA-04 / HA-07). Stored in
// AppHalalApplications.Status as plain nvarchar(60), not an enum, because D-26 states them
// as display strings (for example "PROCESSING AT {CB} (NEW)" carries the CB name).
public static class ApplicationStatus
{
    public const string Draft = "DRAFT";

    public const string ApplicationApproved = "APPLICATION APPROVED";

    public const string AuditInProgress = "AUDIT (IN PROGRESS)";

    public const string AuditCompleted = "AUDIT (COMPLETED)";

    public const string ApprovedWithDocument = "APPROVED WITH DOCUMENT";
}

// My Application (spec 12.3-12.7): one row per halal application. The Survey* bools are the
// saved answers of the Application Survey Form gate (spec 12.4, D-08) shown read-only on the
// General Information tab; only answers matching the D-08 key ever reach this table because
// any other combination is blocked at creation. Company information is read from Company on
// the screen, never copied here (Database.md 10). Tenant [T].
public class ApplicationEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    // VH SMART Reference No., built by IReferenceNumberGenerator (D-09):
    // {Prefix}({SchemeCode})/{ddMMyyyy}/{n}.
    public string ReferenceNo { get; set; } = string.Empty;

    // New or Renewal (Database.md 10); defaults to New at creation, editable on the
    // application screen header (spec 12.5).
    public string ApplicationType { get; set; } = "New";

    public string Status { get; set; } = ApplicationStatus.Draft;

    // Date of the current status - the "Status Date" list column (spec 12.3).
    public DateTime StatusDate { get; set; }

    public Guid SchemeId { get; set; }

    // Chosen on the application screen header ("Batch Selection", spec 12.5); the create
    // flow (12.3) has no batch slot.
    public Guid? BatchId { get; set; }

    public string? CbApplicationNo { get; set; }

    public DateOnly? CbApplicationDate { get; set; }

    public string? HalalCoachName { get; set; }

    // Application Survey Form answers (spec 12.4): Q1, Q2, Q3, Q4 in order. The correct key
    // (D-08) is Yes / Yes / No / Yes, so SurveyHandlesProhibited is stored false.
    public bool SurveyReadProcedureManual { get; set; }

    public bool SurveyReadMs1500 { get; set; }

    public bool SurveyHandlesProhibited { get; set; }

    public bool SurveyHasIhc { get; set; }

    // Company Information tab extras (spec 12.5) - HA-03 fills them.
    public string? YearlySalesRevenue { get; set; }

    public string? ProductMarket { get; set; }

    public TimeOnly? WorkingHourFrom { get; set; }

    public TimeOnly? WorkingHourTo { get; set; }

    public int? NumberOfShifts { get; set; }

    public int? MuslimManagement { get; set; }

    public int? MuslimFoodHandler { get; set; }

    public int? MuslimChef { get; set; }

    public int? NonMuslimManagement { get; set; }

    public int? NonMuslimFoodHandler { get; set; }

    public int? NonMuslimChef { get; set; }

    // Submit Acknowledgement (spec 12.5 / 12.6) - HA-04 fills them.
    public string? AckName { get; set; }

    public string? AckEmail { get; set; }

    public string? AckMobile { get; set; }

    public bool AckAccepted { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public Guid? SubmittedBy { get; set; }
}

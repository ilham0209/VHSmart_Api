namespace VHSmart_Api.Shared.Domain.HalalApplication;

// One row per status change of an application (Database.md 10): creation writes the first
// row (FromStatus null -> DRAFT) and every later change - Submit, "Tagging Application
// Status" - appends another (D-26). Tenant [T] because Database.md marks it so. Unlinking
// is not applicable; the rows are an audit trail and only soft delete with the application.
public class ApplicationStatusHistoryEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid ApplicationId { get; set; }

    // Null on the first row - there was no status before DRAFT.
    public string? FromStatus { get; set; }

    public string ToStatus { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; }

    // The user from the JWT (Guid? per Database.md 10; the claim is a Guid string).
    public Guid? ChangedBy { get; set; }

    public string? Remarks { get; set; }
}

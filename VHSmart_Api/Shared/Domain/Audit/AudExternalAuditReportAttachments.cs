namespace VHSmart_Api.Shared.Domain.Audit;

// Tab "Attachment" of an external audit report (spec 14.10: "not seen" in the manual,
// Database.md 13 defines the table): one stored file per row; bytes live in IFileStorage
// (F-06), the row keeps the metadata (CodingRules 10). Soft delete keeps the bytes, the
// same stance as the staff attachment (house pattern).
public class ExternalAuditReportAttachmentEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid ExternalAuditReportId { get; set; }

    public StoredFile Document { get; set; } = new();
}

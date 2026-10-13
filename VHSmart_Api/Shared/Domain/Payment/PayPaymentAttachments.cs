namespace VHSmart_Api.Shared.Domain.Payment;

// Payment attachments (spec 15.1 "List of Attachments", Database.md 12): PaymentId link,
// a typed DocumentType label and the File column group. Upload date = SysDateCreated
// (Database.md 12). There is no Payment value in SupportingDocumentForView (Database.md 3
// lists exactly the 7 the enum stores), so DocumentType is a required typed string rather
// than a lookup - flagged. Tenant [T].
public class PaymentAttachmentEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid PaymentId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public StoredFile Document { get; set; } = new();
}

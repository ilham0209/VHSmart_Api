using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Shared.Domain.HalalApplication;

// "Halal Application Supporting Document" of the application's Attachment side tab (spec
// 12.5, Database.md 10): one uploaded document, typed by the company's own Supporting
// Document rows with For View = Halal Application (R-06). Like every other attachment table
// this codebase keeps (Raw Material 10.2, Premise 7.7), a row only exists once a file has
// been uploaded, so the File group is required. Tenant [T].
public class ApplicationAttachmentEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid ApplicationId { get; set; }

    public Guid DocumentTypeId { get; set; }

    public SupportingDocumentEntity? DocumentType { get; set; }

    public StoredFile Document { get; set; } = new();
}

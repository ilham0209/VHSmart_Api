using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Shared.Domain.Companies;

// The "Staff Attachment" tab of the Manage Staff modal (spec 7.4): one file per document type
// per staff, the type picked from AdmSupportingDocuments with ForView = AllStaff (Database.md 3).
// Soft delete keeps the row and its bytes (CodingRules 7.1, same as the R-05 icon); there is no
// uniqueness rule documented, so the same type may be uploaded twice.
public class StaffAttachmentEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid StaffId { get; set; }

    public StaffEntity? Staff { get; set; }

    public Guid DocumentTypeId { get; set; }

    public SupportingDocumentEntity? DocumentType { get; set; }

    public StoredFile Document { get; set; } = new();
}

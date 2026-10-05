using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Shared.Domain.RawMaterial;

// Raw Material > Master List, section "Attachment Information" (spec 10.2, Database.md 8):
// one row per (RawMaterialId, DocumentTypeId) holding the uploaded file. The document type is
// a company's own Supporting Document row with For View = Raw Material (R-06) - reference data,
// not the fixed D-15 list the premise tab uses - so the type list follows whatever the company
// created. A row only exists once a file was uploaded, which is why Document is required (the
// "_" File marker of Database.md 8). DocumentStatus is written from HalalStatusCalculator when
// the file is stored, but every read recomputes it from ExpiryDate (D-04) so it cannot go stale.
public class RawMaterialAttachmentEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid RawMaterialId { get; set; }

    public RawMaterialEntity? RawMaterial { get; set; }

    public Guid DocumentTypeId { get; set; }

    public SupportingDocumentEntity? DocumentType { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string? DocumentStatus { get; set; }

    public string? ReferenceNo { get; set; }

    public string? Authority { get; set; }

    public StoredFile Document { get; set; } = new();
}

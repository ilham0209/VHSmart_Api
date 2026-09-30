namespace VHSmart_Api.Shared.Domain.Admin;

// The "For View" selector of the Supporting Document screen (spec 5.5 [CONFIRMED]): the 7
// values of Database.md 3 verbatim, so HasConversion<string>() stores "SopHas" in the column
// and JsonStringEnumConverter sends the same value to the frontend (CodingRules 11).
public enum SupportingDocumentForView
{
    SopDocumentsAndRecords,
    SopHas,
    SopIhcs,
    Training,
    RawMaterial,
    HalalApplication,
    AllStaff
}

// Supporting Document (spec 5.5): per-company reference data [T] that controls which document
// types the other modules request (Raw Material, All Staff, Halal Application, Training - the
// DocumentTypeId FKs of Database.md 3), their order and whether they are mandatory, and holds
// the SOP / HAS / IHCS templates. Not seeded; rows are created through the API.
public class SupportingDocumentEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public SupportingDocumentForView ForView { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public int DocumentSequence { get; set; }

    public bool IsMandatory { get; set; }

    public string? Description { get; set; }

    // File column group (Database.md 3), optional: only the SOP views carry a template
    // (spec 5.5 "template upload for SOP views"). Bytes live in IFileStorage (F-06), the row
    // carries only the metadata.
    public StoredFile? Template { get; set; }
}

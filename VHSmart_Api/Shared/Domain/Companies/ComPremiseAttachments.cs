namespace VHSmart_Api.Shared.Domain.Companies;

// Premise > Manage Premise (spec 7.7 tab "Premise Attachment"): one current row per
// (PremiseId, DocumentType) with the uploaded PDF, its expiry and reference number
// (Database.md 7). DocumentType is one of the D-15 fixed list - validated by the upload
// handler, not by a lookup table. Document is required (the "_" File marker): a row only
// exists once a file was uploaded, which is also how the calculator sees "has upload".
public class PremiseAttachmentEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid PremiseId { get; set; }

    public PremiseEntity? Premise { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public DateTime? ExpiryDate { get; set; }

    public string? ReferenceNo { get; set; }

    public StoredFile Document { get; set; } = new();
}

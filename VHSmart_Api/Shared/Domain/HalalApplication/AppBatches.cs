namespace VHSmart_Api.Shared.Domain.HalalApplication;

// Halal Application > Manage Batch (spec 12.2, Database.md 10 "AppBatches" [T]): one row per
// batch - the container a halal application applies for. A product-scheme batch groups
// products + a manufacturer; a Food Premise-scheme batch groups premises + a brand (D-15:
// only premises with COMPLETE DOCUMENTATION are selectable). SchemeId/Name are required
// (Database.md 10 markers); BrandId is required by the legacy rule "Brand Owner is required
// in every case" (enforced in the validator, the column stays nullable to match the schema).
// ManufacturerSupplierId is required only for product schemes (enforced in the validator).
// UQ (CompanyId, Name) among live rows - "Error! Please provide unique batch name".
public class BatchEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid SchemeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? CbReferenceNo { get; set; }

    public DateTime? SubmissionPlannedDate { get; set; }

    public Guid? BrandId { get; set; }

    public Guid? ManufacturerSupplierId { get; set; }

    public string? Description { get; set; }
}

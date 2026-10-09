namespace VHSmart_Api.Shared.Domain.HalalApplication;

// One premise linked to a Food Premise-scheme batch (spec 12.2 "Associate Premise",
// Database.md 10 "AppBatchPremises" [T]). Unlike AppBatchProducts there is no MappingStatus
// column - the table is a simple association, so unlinking soft-deletes the row and the
// filtered unique index (BatchId, PremiseId) frees the slot (same stance as
// ComCompanyBrands). D-15: only premises with COMPLETE DOCUMENTATION are selectable.
public class BatchPremiseEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid BatchId { get; set; }

    public Guid PremiseId { get; set; }
}

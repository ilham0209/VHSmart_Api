namespace VHSmart_Api.Shared.Domain.HalalApplication;

// One product linked to a batch (spec 12.2 edit modal "List of Products", Database.md 10
// "AppBatchProducts" [T]). The Action cell links and unlinks by toggling MappingStatus - the
// same stance as PrdProductIngredients (PD-02): unlinking keeps the row, so the filtered
// unique index (BatchId, ProductId) still describes one live row per pair and a re-link just
// flips it back. ACTIVE/INACTIVE are the spec's values (the edit modal shows "ACTIVE").
public static class BatchProductMappingStatus
{
    public const string Active = "ACTIVE";

    public const string Inactive = "INACTIVE";
}

public class BatchProductEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid BatchId { get; set; }

    public Guid ProductId { get; set; }

    public string MappingStatus { get; set; } = BatchProductMappingStatus.Active;
}

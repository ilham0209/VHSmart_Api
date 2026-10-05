namespace VHSmart_Api.Shared.Domain.Product;

// Manage Ingredient Information tab (spec 9.1, Database.md 9 "PrdProductIngredients"): one row
// per product / raw-material pair. The tab's Action cell links and unlinks, and Database.md 9
// says that "Link/unlink toggles MappingStatus" - so unlinking keeps the row and only flips the
// status, it is not a soft delete (the filtered unique index (ProductId, RawMaterialId) then
// still describes one live row per pair and a re-link just flips it back). The spec shows
// "ACTIVE" as the sample value and implies a second one; INACTIVE is our name for it (flagged
// in the report - the owner may rename it, the column is a 20-char string).
public static class ProductIngredientMappingStatus
{
    public const string Active = "ACTIVE";

    public const string Inactive = "INACTIVE";
}

public class ProductIngredientEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid ProductId { get; set; }

    public Guid RawMaterialId { get; set; }

    public string MappingStatus { get; set; } = ProductIngredientMappingStatus.Active;
}

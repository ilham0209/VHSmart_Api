namespace VHSmart_Api.Shared.Domain.Product;

// Manage Product (spec 9.1, Database.md 9 "PrdProducts"): the Common Product Information
// form of one company ("For Company" comes from the JWT like every other screen). The three
// list columns - Ingredient Link Status, Halal Status and Expiry - are DERIVED (Database.md
// 9 says so) and need PrdProductIngredients, which PD-02 creates - they are not columns here.
// SchemeSpecificData stays null until the scheme-specific fields are seen (9.1 blue note),
// QrCodeKey null is the form's "QR Code yet to be generated", and VerifyHalalPublishStatus
// belongs to PD-06 (Verify Halal Product Update) - this task only creates the column.
public class ProductEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid SchemeId { get; set; }

    // Required on the spec form (Name*); Database.md 9 keeps the column nullable.
    public string? Name { get; set; }

    // Required on the spec form (Manufacturer*); the dropdown lists RawManufacturerSuppliers
    // rows that carry a manufacturer half.
    public Guid? ManufacturerSupplierId { get; set; }

    public Guid BrandId { get; set; }

    public Guid CategoryId { get; set; }

    // Semicolon separated when a product has more than one code (spec 9.1).
    public string? Code { get; set; }

    public string? Gtin { get; set; }

    public string? NutritionContentClaims { get; set; }

    public string? PotentialAllergens { get; set; }

    public string? CalorieContent { get; set; }

    public string? AvailableAt { get; set; }

    public string? PackagingSize { get; set; }

    public Guid? MarketingMethodId { get; set; }

    // nvarchar(max) JSON of the scheme-specific fields (Database.md 9). Null until the spec
    // shows those fields (9.1 "not yet seen") - never read or written by this task.
    public string? SchemeSpecificData { get; set; }

    public string? QrCodeKey { get; set; }

    // Published / PublishedWithoutImage (Database.md 9); written by PD-06 only.
    public string? VerifyHalalPublishStatus { get; set; }
}

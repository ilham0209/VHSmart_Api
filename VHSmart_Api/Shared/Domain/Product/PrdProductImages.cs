namespace VHSmart_Api.Shared.Domain.Product;

// Spec 9.1 guidelines: one image per angle - front, back, left, right (legacy document types
// 401/402/403/404 map to positions 1/2/3/4 in that order). Stored as its string
// (CodingRules 11).
public enum ProductImagePosition
{
    Front,
    Back,
    Left,
    Right
}

// Tab "Manage Attachment Information" (spec 9.1, Database.md 9 "PrdProductImages"): the four
// angles of one product, each exactly 1200 x 1200 px JPEG/JPG/PNG (D-22). Replacing an angle
// does not overwrite the row: the old row is kept with IsCurrent = false and a NEW row carries
// Version + 1 and IsCurrent = true, which is what "Replacing an image increments Version and
// flips IsCurrent" (Database.md 9) describes. The tab therefore shows the current rows only.
public class ProductImageEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid ProductId { get; set; }

    public ProductImagePosition Position { get; set; }

    // 1 for the first upload of an angle, +1 for every replacement (never reused, even after
    // the current row is deleted).
    public int Version { get; set; }

    public bool IsCurrent { get; set; }

    public StoredFile Image { get; set; } = new();
}

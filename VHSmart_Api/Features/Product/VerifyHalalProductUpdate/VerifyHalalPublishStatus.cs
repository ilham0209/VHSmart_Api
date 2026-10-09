namespace VHSmart_Api.Features.Product.VerifyHalalProductUpdate;

// The two stored values of PrdProducts.VerifyHalalPublishStatus (Database.md 9):
// "Published" and "PublishedWithoutImage". The spec 9.4 shows the second one with a
// client-side link "Please update product image"; the stored value has no space so it
// fits the nvarchar(30) column cleanly. Never repeat these strings elsewhere (D-05).
public static class VerifyHalalPublishStatus
{
    public const string Published = "Published";

    public const string PublishedWithoutImage = "PublishedWithoutImage";
}

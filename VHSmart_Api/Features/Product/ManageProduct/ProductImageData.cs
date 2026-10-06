using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Product.ManageProduct;

// Tab "Manage Attachment Information" (spec 9.1): the four image angles and the document type
// labels printed on the upload form. Database.md 9 stores only the Position enum, so the label
// is derived from it - there is no Supporting Document row behind it (R-06's "For View" list has
// no product view), which is why the four labels live in code instead of reference data.
internal static class ProductImageData
{
    // The order of the spec guidelines box: "front, back, left, right" (legacy document types
    // 401/402/403/404 map to positions 1/2/3/4 in the same order).
    public static readonly ProductImagePosition[] Positions =
        [ProductImagePosition.Front, ProductImagePosition.Back, ProductImagePosition.Left, ProductImagePosition.Right];

    public static int Order(ProductImagePosition position) => Array.IndexOf(Positions, position);

    // D-22: product images are exactly 1,200 x 1,200 px, JPEG / JPG / PNG, max 10 MB - checked
    // by the upload before anything is stored.
    public const int RequiredWidth = 1200;
    public const int RequiredHeight = 1200;

    public static string DocumentType(ProductImagePosition position) =>
        $"PRODUCT IMAGES ({position.ToString().ToUpperInvariant()})";

    // The form's Document Type* dropdown sends the label; a client may also send the position
    // itself ("Front"). Both spellings are accepted case-insensitively, everything else is not
    // one of the four angles this table can hold.
    public static bool TryParseDocumentType(string? documentType, out ProductImagePosition position)
    {
        position = default;
        var normalized = documentType?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized))
            return false;

        foreach (var candidate in Positions)
        {
            if (normalized == DocumentType(candidate).ToUpperInvariant()
                || normalized == candidate.ToString().ToUpperInvariant())
            {
                position = candidate;
                return true;
            }
        }

        return false;
    }

    public static async Task<ProductEntity> GetProductAsync(
        VHSmartDbContext db,
        ICurrentUser user,
        Guid productId,
        CancellationToken ct)
    {
        var product = await db.Products
            .FirstOrDefaultAsync(
                row => row.Id == productId && row.CompanyId == user.CompanyId, ct);

        if (product is null)
            throw new NotFoundException("Product not found.");

        return product;
    }

    // D-22 product-image half: the file itself decides, never the browser's Content-Type. The
    // extension and size limits are checked by the upload handler first (FileValidation), this
    // reads the pixel size from the buffered bytes - still before a byte reaches storage.
    public static ImageSize ValidateDimensions(ReadOnlySpan<byte> bytes)
    {
        var size = ImageDimensions.TryRead(bytes);
        if (size is null)
            throw new BusinessRuleException("The file is not a valid image.");

        if (size.Value.Width != RequiredWidth || size.Value.Height != RequiredHeight)
            throw new BusinessRuleException(
                $"The image must be exactly {RequiredWidth} x {RequiredHeight} pixels.");

        return size.Value;
    }
}

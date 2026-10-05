using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageProduct;

// Spec 6.3 / 9.1 [CONFIRMED]: "Please assign at least one Brand (Manage Brand Information) to
// retrieve the Ingredient Information" - a company with no brand link cannot read the
// Ingredient Information tab at all, so every endpoint of that tab calls this first. The link
// itself lives in ComCompanyBrands (D-20, owned by Manage Companies): this only reads it, the
// product's own BrandId is a different thing (the brand the form picked for the product).
internal static class ProductIngredientData
{
    public const string BrandRequiredMessage =
        "Please assign at least one Brand (Manage Brand Information) to retrieve the Ingredient Information";

    public static async Task EnsureBrandLinkedAsync(
        VHSmartDbContext db,
        ICurrentUser user,
        CancellationToken ct)
    {
        var hasBrand = await db.CompanyBrands
            .AsNoTracking()
            .AnyAsync(row => row.CompanyId == user.CompanyId, ct);

        if (!hasBrand)
            throw new BusinessRuleException(BrandRequiredMessage);
    }
}

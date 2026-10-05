using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Product.ManageProduct;

// The three DERIVED columns of the product list (spec 9.1, Database.md 9 "Ingredient Link
// Status, Halal Status and Expiry are derived", D-18). Nothing is stored: the values are
// computed for a page AFTER paging, so they never take part in the search or the sort - the
// same stance the raw material list takes for its halal column.
//
// D-18: a product is Valid when it has >= 1 linked (ACTIVE) ingredient AND every one of those
// raw materials has a HALAL CERTIFICATE that is not expired. Anything else is Expired - D-18
// and the spec column know no third word. Product expiry = the earliest expiry of its linked
// raw materials; a material with no certificate (or no expiry) contributes no date, so a page
// full of them answers null and the column renders empty.
public record ProductHalalInfoResponse(
    bool IsIngredientLinked,
    HalalStatus HalalStatus,
    DateOnly? ExpiryDate);

// The words of the "Ingredient Link Status" column (spec 9.1): the spec prints "Linked" and
// shows a warning icon for the other case, so UNLINKED is our text for it - a client that
// wants the icon tests the value instead of re-deriving it.
public static class ProductIngredientLinkStatus
{
    public const string Linked = "Linked";

    public const string Unlinked = "Unlinked";
}

internal static class ProductHalalInformation
{
    // What a product with no linked ingredient answers: unlinked, and therefore not Valid -
    // D-18 only calls a product Valid when it has at least one linked raw material.
    public static readonly ProductHalalInfoResponse NoIngredient = new(
        IsIngredientLinked: false,
        HalalStatus: HalalStatus.Expired,
        ExpiryDate: null);

    public static async Task<IReadOnlyDictionary<Guid, ProductHalalInfoResponse>> LoadManyAsync(
        VHSmartDbContext db,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken ct)
    {
        if (productIds.Count == 0)
            return new Dictionary<Guid, ProductHalalInfoResponse>();

        var links = await db.ProductIngredients
            .AsNoTracking()
            .Where(row => productIds.Contains(row.ProductId)
                && row.MappingStatus == ProductIngredientMappingStatus.Active)
            .Select(row => new { row.ProductId, row.RawMaterialId })
            .ToListAsync(ct);

        // A raw material that was deleted or unshared after the link is invisible under the
        // 7.3 filter, and the tab drops that row too - the list must not call a product Valid
        // on the strength of an ingredient its own table no longer shows.
        var rawMaterialIds = links.Select(row => row.RawMaterialId).Distinct().ToList();
        var visibleRawMaterialIds = (await db.RawMaterials
                .AsNoTracking()
                .Where(row => rawMaterialIds.Contains(row.Id))
                .Select(row => row.Id)
                .ToListAsync(ct))
            .ToHashSet();
        links = [.. links.Where(row => visibleRawMaterialIds.Contains(row.RawMaterialId))];

        // The certificate rule lives with the raw material screen (spec 10.2 / D-04) so both
        // lists answer the same Valid / Expired for the same file.
        var certificates = await RawMaterialHalalInformation.LoadManyAsync(
            db,
            links.Select(row => row.RawMaterialId).Distinct().ToList(),
            ct);

        var result = new Dictionary<Guid, ProductHalalInfoResponse>(productIds.Count);
        foreach (var productId in productIds.Distinct())
        {
            var productLinks = links.Where(row => row.ProductId == productId).ToList();
            if (productLinks.Count == 0)
            {
                result[productId] = NoIngredient;
                continue;
            }

            var isValid = true;
            DateOnly? expiry = null;
            foreach (var link in productLinks)
            {
                var certificate = certificates.GetValueOrDefault(link.RawMaterialId);
                if (certificate is null || certificate.Status != HalalStatus.Valid)
                    isValid = false;

                if (certificate?.ExpiryDate is { } candidate
                    && (expiry is null || candidate < expiry.Value))
                    expiry = candidate;
            }

            result[productId] = new ProductHalalInfoResponse(
                IsIngredientLinked: true,
                HalalStatus: isValid ? HalalStatus.Valid : HalalStatus.Expired,
                ExpiryDate: expiry);
        }

        return result;
    }
}

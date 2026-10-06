using VHSmart_Api.Shared.Domain.Product;

namespace VHSmart_Api.Features.Product.ManageProduct;

// One place where a command becomes a row, so Create and Update can never disagree about the
// mapping (spec 9.1 "Common Product Information"). The three required Guids arrive nullable
// so a missing value fails validation instead of silently becoming Guid.Empty; the validators
// run before any handler (ValidationBehavior), so the .Value reads below are safe. The
// derived columns (Ingredient Link Status, Halal Status, Expiry) and SchemeSpecificData are
// never assigned here - see PrdProducts.
internal static class ProductData
{
    public static ProductEntity Apply(
        ProductEntity entity,
        CreateProductCommand request) =>
        Assign(
            entity, request.SchemeId, request.Name, request.ManufacturerSupplierId,
            request.BrandId, request.CategoryId, request.Code, request.Gtin,
            request.NutritionContentClaims, request.PotentialAllergens,
            request.CalorieContent, request.AvailableAt, request.PackagingSize,
            request.MarketingMethodId);

    public static ProductEntity Apply(
        ProductEntity entity,
        UpdateProductCommand request) =>
        Assign(
            entity, request.SchemeId, request.Name, request.ManufacturerSupplierId,
            request.BrandId, request.CategoryId, request.Code, request.Gtin,
            request.NutritionContentClaims, request.PotentialAllergens,
            request.CalorieContent, request.AvailableAt, request.PackagingSize,
            request.MarketingMethodId);

    private static ProductEntity Assign(
        ProductEntity entity,
        Guid? schemeId,
        string? name,
        Guid? manufacturerSupplierId,
        Guid? brandId,
        Guid? categoryId,
        string? code,
        string? gtin,
        string? nutritionContentClaims,
        string? potentialAllergens,
        string? calorieContent,
        string? availableAt,
        string? packagingSize,
        Guid? marketingMethodId)
    {
        entity.SchemeId = schemeId!.Value;
        entity.Name = name;
        entity.ManufacturerSupplierId = manufacturerSupplierId;
        entity.BrandId = brandId!.Value;
        entity.CategoryId = categoryId!.Value;
        entity.Code = code;
        entity.Gtin = gtin;
        entity.NutritionContentClaims = nutritionContentClaims;
        entity.PotentialAllergens = potentialAllergens;
        entity.CalorieContent = calorieContent;
        entity.AvailableAt = availableAt;
        entity.PackagingSize = packagingSize;
        entity.MarketingMethodId = marketingMethodId;

        return entity;
    }
}

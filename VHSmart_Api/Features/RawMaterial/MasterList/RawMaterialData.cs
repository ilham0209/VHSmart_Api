using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.RawMaterial.MasterList;

// One place where a command becomes a row, so Create and Update can never disagree about the
// mapping (spec 10.2 form fields). Ingredient code is normalized: a blank code is stored as
// NULL so it never collides with another blank code in the filtered unique index (D-17).
internal static class RawMaterialData
{
    public static RawMaterialEntity Apply(
        RawMaterialEntity entity,
        CreateRawMaterialCommand request) =>
        Assign(
            entity, request.Category, request.IngredientStatusId, request.Ingredient,
            request.IngredientCode, request.CommercialName, request.ScientificName,
            request.IngredientSourceId, request.ManufacturerSupplierId,
            request.IsPackagingMaterial);

    public static RawMaterialEntity Apply(
        RawMaterialEntity entity,
        UpdateRawMaterialCommand request) =>
        Assign(
            entity, request.Category, request.IngredientStatusId, request.Ingredient,
            request.IngredientCode, request.CommercialName, request.ScientificName,
            request.IngredientSourceId, request.ManufacturerSupplierId,
            request.IsPackagingMaterial);

    private static RawMaterialEntity Assign(
        RawMaterialEntity entity,
        RawMaterialCategory? category,
        Guid? ingredientStatusId,
        string? ingredient,
        string? ingredientCode,
        string? commercialName,
        string? scientificName,
        Guid? ingredientSourceId,
        Guid? manufacturerSupplierId,
        bool isPackagingMaterial)
    {
        entity.Category = category!.Value;
        entity.IngredientStatusId = ingredientStatusId!.Value;
        entity.Ingredient = ingredient;
        entity.IngredientCode = string.IsNullOrWhiteSpace(ingredientCode) ? null : ingredientCode.Trim();
        entity.CommercialName = commercialName;
        entity.ScientificName = scientificName;
        entity.IngredientSourceId = ingredientSourceId;
        entity.ManufacturerSupplierId = manufacturerSupplierId!.Value;
        entity.IsPackagingMaterial = isPackagingMaterial;

        return entity;
    }

    // The visibility filter (CodingRules 7.3) also shows rows shared WITH the caller, but
    // sharing is read-only: only the owner company may edit or delete the row - and a Switch
    // Company = ALL token, which the tenant filter already trusts everywhere else. Anything
    // else answers 404 so existence is never revealed (CodingRules 9).
    public static void EnsureOwner(RawMaterialEntity entity, ICurrentUser user)
    {
        var isOwner = entity.CompanyId == user.CompanyId;
        var isViewAll = user.IsPlatformAdmin || user.ViewAllCompanies;

        if (!isOwner && !isViewAll)
            throw new NotFoundException("Raw material not found.");
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Product.ManageProduct;

// The source of the tab's "+ Add New Ingredient" button (spec 9.1): the raw materials the
// caller may link, minus the ones this product already has a live row for (an UNLINKED pair is
// not excluded - it is on the tab with its own link icon, and LinkProductIngredient flips it
// back). Visibility is the "Accessible For" filter of CodingRules 7.3, so a material shared
// with the caller is offered and a foreign, unshared one is not. Its halal certificate status
// is filled after paging: derived data never takes part in the search or the sort (RM-02
// stance). The tab is behind the same >= 1 brand rule as the list (spec 6.3 / 9.1).
public record GetProductIngredientOptionsQuery(Guid ProductId)
    : IRequest<DataGridResponse<ProductIngredientOptionResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record ProductIngredientOptionResponse(
    Guid Id,
    string? Ingredient,
    string? IngredientCode,
    string? ManufacturerName,
    HalalStatus? HalalCertificateStatus);

public class GetProductIngredientOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetProductIngredientOptionsQuery, DataGridResponse<ProductIngredientOptionResponse>>
{
    public async Task<DataGridResponse<ProductIngredientOptionResponse>> Handle(
        GetProductIngredientOptionsQuery request,
        CancellationToken ct)
    {
        var productExists = await db.Products
            .AsNoTracking()
            .AnyAsync(row => row.Id == request.ProductId && row.CompanyId == user.CompanyId, ct);
        if (!productExists)
            throw new NotFoundException("Product not found.");

        await ProductIngredientData.EnsureBrandLinkedAsync(db, user, ct);

        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(ProductIngredientOptionResponse.Ingredient)
            : request.Request.SortBy;

        var linkedRawMaterialIds = db.ProductIngredients
            .AsNoTracking()
            .Where(row => row.ProductId == request.ProductId)
            .Select(row => row.RawMaterialId);

        var grid = await db.RawMaterials
            .AsNoTracking()
            .Where(row => !linkedRawMaterialIds.Contains(row.Id))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(RawMaterialEntity.Ingredient),
                nameof(RawMaterialEntity.IngredientCode),
                nameof(RawMaterialEntity.CommercialName),
                nameof(RawMaterialEntity.ScientificName))
            .Select(row => new ProductIngredientOptionResponse(
                row.Id,
                row.Ingredient,
                row.IngredientCode,
                db.ManufacturerSuppliers
                    .Where(supplier => supplier.Id == row.ManufacturerSupplierId)
                    .Select(supplier => supplier.ManufacturerName ?? supplier.SupplierName)
                    .FirstOrDefault(),
                null))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);

        var certificates = await RawMaterialHalalInformation.LoadManyAsync(
            db, [.. grid.Data.Select(row => row.Id)], ct);
        grid.Data = [.. grid.Data.Select(
            row => row with { HalalCertificateStatus = certificates.GetValueOrDefault(row.Id)?.Status })];

        return grid;
    }
}

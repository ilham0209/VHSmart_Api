using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Product.ManageMenu;

// The source of the modal's "List of Raw Materials*" link button (spec 9.2): the raw materials
// the caller may attach to this menu, minus the ones the menu already has a live link row for
// (a pair dropped by an earlier save is soft deleted, so the material is offered again).
// Columns are exactly the spec's: Ingredient, Scientific Name, Manufacturer/Supplier
// Information, Halal Information - the same record the attached list returns. Visibility is the
// "Accessible For" filter of CodingRules 7.3, so a material shared with the caller is offered
// and a foreign, unshared one is not; only the owner can actually save the menu. The halal
// certificate values are filled after paging: derived data never takes part in the search or
// the sort (RM-02 stance).
public record GetMenuIngredientOptionsQuery(Guid MenuId)
    : IRequest<DataGridResponse<MenuIngredientResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public class GetMenuIngredientOptionsHandler(VHSmartDbContext db)
    : IRequestHandler<GetMenuIngredientOptionsQuery, DataGridResponse<MenuIngredientResponse>>
{
    public async Task<DataGridResponse<MenuIngredientResponse>> Handle(
        GetMenuIngredientOptionsQuery request,
        CancellationToken ct)
    {
        // The visibility filter (CodingRules 7.3) makes an unknown, foreign or unshared menu
        // answer 404 before anything is offered for it.
        var menuExists = await db.Menus
            .AsNoTracking()
            .AnyAsync(row => row.Id == request.MenuId, ct);
        if (!menuExists)
            throw new NotFoundException("Menu not found.");

        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(MenuIngredientResponse.Ingredient)
            : request.Request.SortBy;

        var linkedRawMaterialIds = db.MenuRawMaterials
            .AsNoTracking()
            .Where(row => row.MenuId == request.MenuId)
            .Select(row => row.RawMaterialId);

        var grid = await db.RawMaterials
            .AsNoTracking()
            .Where(row => !linkedRawMaterialIds.Contains(row.Id))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(RawMaterialEntity.Ingredient),
                nameof(RawMaterialEntity.ScientificName),
                nameof(RawMaterialEntity.CommercialName))
            .Select(row => new MenuIngredientResponse(
                row.Id,
                row.Ingredient,
                row.ScientificName,
                new MenuIngredientManufacturerInformation(
                    db.ManufacturerSuppliers
                        .Where(supplier => supplier.Id == row.ManufacturerSupplierId)
                        .Select(supplier => supplier.ManufacturerName ?? supplier.SupplierName)
                        .FirstOrDefault(),
                    db.ManufacturerSuppliers
                        .Where(supplier => supplier.Id == row.ManufacturerSupplierId)
                        .Select(supplier => supplier.ManufacturerAddress ?? supplier.SupplierAddress)
                        .FirstOrDefault(),
                    db.ManufacturerSuppliers
                        .Where(supplier => supplier.Id == row.ManufacturerSupplierId)
                        .Select(supplier => supplier.ManufacturerContactNo ?? supplier.SupplierContactNo)
                        .FirstOrDefault()),
                null))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);

        var certificates = await RawMaterialHalalInformation.LoadManyAsync(
            db, [.. grid.Data.Select(row => row.Id)], ct);
        grid.Data = [.. grid.Data.Select(
            row => row with { HalalInformation = certificates.GetValueOrDefault(row.Id) })];

        return grid;
    }
}

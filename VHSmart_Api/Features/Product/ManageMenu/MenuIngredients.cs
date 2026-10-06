using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Product.ManageMenu;

// "List of Raw Materials" of the menu (spec 9.2 columns): Action is client side, the rest is
// joined display data - the same shape the picker (GET {id}/ingredients/options) returns, so
// the modal's attached table and its "+ " popup are served by one record type.
// "Manufacturer/Supplier Information" is the raw material's manufacturer, falling back to its
// supplier half so a row that only knows a supplier is not empty (the PD-01/PD-02 stance).
public record MenuIngredientManufacturerInformation(
    string? Name,
    string? Address,
    string? Contact);

public record MenuIngredientResponse(
    Guid Id,
    string? Ingredient,
    string? ScientificName,
    MenuIngredientManufacturerInformation ManufacturerInformation,
    RawMaterialHalalInfoResponse? HalalInformation);

internal static class MenuIngredientListLoader
{
    // The ingredient table of ONE menu, ordered by ingredient name. The link rows belong to
    // the menu's company, so a company the menu was shared WITH would otherwise see an empty
    // list - they are read IgnoreQueryFilters but always scoped to this menu id, and the
    // soft-delete flag is checked by hand because the filter is off. The raw materials are read
    // the same way: the ingredient list travels WITH the menu (CodingRules 7.3 shares the row,
    // not a hole in it). The PICKER keeps the 7.3 filter - it is what the caller may attach,
    // and only the owner ever writes.
    public static async Task<IReadOnlyList<MenuIngredientResponse>> LoadAsync(
        VHSmartDbContext db,
        Guid menuId,
        CancellationToken ct)
    {
        var links = await db.MenuRawMaterials
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => row.MenuId == menuId && !row.IsDeleted)
            .Select(row => new { row.RawMaterialId })
            .ToListAsync(ct);

        var rawMaterialIds = links.Select(row => row.RawMaterialId).Distinct().ToList();
        if (rawMaterialIds.Count == 0)
            return [];

        var materials = await db.RawMaterials
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => rawMaterialIds.Contains(row.Id) && !row.IsDeleted)
            .Select(row => new
            {
                row.Id,
                row.Ingredient,
                row.ScientificName,
                row.ManufacturerSupplierId
            })
            .ToListAsync(ct);

        // Derived data of THIS menu (spec 9.2): the certificate is read through the shared
        // helper, so a menu shared with another company shows its ingredient rows without the
        // owner's certificate rows - the documented stance of RM-02's certificate join.
        var certificates = await RawMaterialHalalInformation.LoadManyAsync(db, rawMaterialIds, ct);

        var manufacturerIds = materials
            .Select(row => row.ManufacturerSupplierId)
            .Distinct()
            .ToList();
        var manufacturers = await db.ManufacturerSuppliers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => manufacturerIds.Contains(row.Id) && !row.IsDeleted)
            .Select(row => new
            {
                row.Id,
                Name = row.ManufacturerName ?? row.SupplierName,
                Address = row.ManufacturerAddress ?? row.SupplierAddress,
                Contact = row.ManufacturerContactNo ?? row.SupplierContactNo
            })
            .ToListAsync(ct);
        var manufacturerById = manufacturers.ToDictionary(row => row.Id);

        var materialById = materials.ToDictionary(row => row.Id);

        return links
            .Where(link => materialById.ContainsKey(link.RawMaterialId))
            .Select(link =>
            {
                var material = materialById[link.RawMaterialId];
                var manufacturer = manufacturerById.GetValueOrDefault(material.ManufacturerSupplierId);

                return new MenuIngredientResponse(
                    material.Id,
                    material.Ingredient,
                    material.ScientificName,
                    new MenuIngredientManufacturerInformation(
                        manufacturer?.Name,
                        manufacturer?.Address,
                        manufacturer?.Contact),
                    certificates.GetValueOrDefault(material.Id));
            })
            .OrderBy(row => row.Ingredient, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Id)
            .ToList();
    }

    // The "List of Ingredients" column of the list (spec 9.2): ingredient names per menu for
    // one page, loaded after paging so the search and the sort only ever touch scalar columns
    // (RM-02 stance) and so a collection never decides the order of the rows.
    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> LoadNamesAsync(
        VHSmartDbContext db,
        IReadOnlyCollection<Guid> menuIds,
        CancellationToken ct)
    {
        if (menuIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<string>>();

        var links = await db.MenuRawMaterials
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => menuIds.Contains(row.MenuId) && !row.IsDeleted)
            .Select(row => new { row.MenuId, row.RawMaterialId })
            .ToListAsync(ct);

        var rawMaterialIds = links.Select(row => row.RawMaterialId).Distinct().ToList();
        var materials = await db.RawMaterials
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => rawMaterialIds.Contains(row.Id) && !row.IsDeleted)
            .Select(row => new { row.Id, row.Ingredient })
            .ToListAsync(ct);
        var nameById = materials.ToDictionary(
            row => row.Id, row => row.Ingredient ?? string.Empty);

        return links
            .Where(link => nameById.ContainsKey(link.RawMaterialId))
            .GroupBy(link => link.MenuId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(link => nameById[link.RawMaterialId])
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList());
    }
}

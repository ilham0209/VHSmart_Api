using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageMenuConcept;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Premise.ManagePremise;

// Menu Information (Current) / (Approved) tabs of the premise modal (spec 7.7 [CONFIRMED]:
// rows # / Menu / Brand / Ingredient Information (Ingredient Name, Manufacturer Name,
// Reference No., Authority, Expiry Date, Status, "Download Halal Certificate"); the Approved
// tab "shows the same structure for items already approved in an application; empty if
// none"). One flat row per menu x raw material - a menu with no visible material still gets
// one row with empty ingredient cells so it stays listed.
//
// Current menus = the premise's MenuConcept's ACTIVE links (Database.md 9; the same stance
// as the concept's own "Menu" column - a menu deleted after linking drops out through
// MenuConceptData). The Brand cell is the premise's brand: menus carry no brand of their
// own and spec 7.7 says Brand sits on the premise for Restaurants & Cafe (flagged).
// Approved = the same menus, but only while the premise itself has an approved item
// (AppCertificateItems.PremiseId - Database.md 12, one snapshot row per approved premise);
// without one the tab is empty, exactly as the spec words it.
// Certificate columns reuse RawMaterialHalalInformation (D-04 + the HALAL CERTIFICATE
// attachment rule of Database.md 13).
public record GetPremiseMenuInformationQuery(Guid PremiseId, bool Approved)
    : IRequest<IReadOnlyList<PremiseMenuInformationRow>>;

public record PremiseMenuInformationRow(
    Guid MenuId,
    string MenuName,
    string? Brand,
    Guid? RawMaterialId,
    string? Ingredient,
    string? ManufacturerName,
    string? ReferenceNo,
    string? Authority,
    DateOnly? ExpiryDate,
    HalalStatus? Status);

public class GetPremiseMenuInformationHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetPremiseMenuInformationQuery, IReadOnlyList<PremiseMenuInformationRow>>
{
    public async Task<IReadOnlyList<PremiseMenuInformationRow>> Handle(
        GetPremiseMenuInformationQuery request,
        CancellationToken ct)
    {
        var premise = await db.Premises
            .AsNoTracking()
            .Where(row => row.Id == request.PremiseId && row.CompanyId == user.CompanyId)
            .Select(row => new { row.MenuConceptId, row.BrandId })
            .FirstOrDefaultAsync(ct);
        if (premise is null)
            throw new NotFoundException("Premise not found.");
        if (premise.MenuConceptId is null)
            return [];

        // "Items already approved in an application": the certificate item the approval
        // hook wrote for THIS premise. No item -> nothing to show, never the Current rows
        // under a different name.
        if (request.Approved)
        {
            var hasApprovedItem = await db.CertificateItems
                .AsNoTracking()
                .AnyAsync(row => row.PremiseId == request.PremiseId
                    && row.CompanyId == user.CompanyId, ct);
            if (!hasApprovedItem)
                return [];
        }

        var links = await db.MenuConceptMenus
            .AsNoTracking()
            .Where(row => row.MenuConceptId == premise.MenuConceptId.Value
                && row.CompanyId == user.CompanyId
                && row.MappingStatus == MenuConceptMenuMappingStatus.Active)
            .Select(row => row.MenuId)
            .Distinct()
            .ToListAsync(ct);
        if (links.Count == 0)
            return [];

        // Names only for menus that still exist (deleted menus drop out), the same helper
        // the Manage Menu Concept screen uses for its "Menu" column.
        var menuInfo = await MenuConceptData.LoadMenuInfoAsync(db, links, ct);
        var menuNames = links
            .Where(menuId => menuInfo.ContainsKey(menuId))
            .Select(menuId => (MenuId: menuId, Name: menuInfo[menuId].Name ?? string.Empty))
            .ToList();
        if (menuNames.Count == 0)
            return [];

        var materials = await db.MenuRawMaterials
            .AsNoTracking()
            .Where(row => menuNames.Select(menu => menu.MenuId).Contains(row.MenuId))
            .Select(row => new { row.MenuId, row.RawMaterialId })
            .ToListAsync(ct);

        var rawMaterialIds = materials
            .Select(row => row.RawMaterialId)
            .Distinct()
            .ToList();
        var rawMaterials = await db.RawMaterials
            .AsNoTracking()
            .Where(row => rawMaterialIds.Contains(row.Id))
            .Select(row => new { row.Id, row.Ingredient, row.ManufacturerSupplierId })
            .ToListAsync(ct);
        var certificates = await RawMaterialHalalInformation.LoadManyAsync(
            db, rawMaterialIds, ct);
        var manufacturerById = await LoadManufacturerNamesAsync(
            db, rawMaterials.Select(row => row.ManufacturerSupplierId).Distinct().ToList(), ct);
        var materialById = rawMaterials.ToDictionary(row => row.Id);

        var brand = premise.BrandId is null
            ? null
            : await db.GeneralData
                .AsNoTracking()
                .Where(row => row.Id == premise.BrandId)
                .Select(row => row.Name)
                .FirstOrDefaultAsync(ct);

        return menuNames
            .SelectMany(menu =>
            {
                var rows = materials
                    .Where(material => material.MenuId == menu.MenuId
                        && materialById.ContainsKey(material.RawMaterialId))
                    .Select(material =>
                    {
                        var value = materialById[material.RawMaterialId];
                        var manufacturer = manufacturerById.GetValueOrDefault(
                            value.ManufacturerSupplierId);
                        var certificate = certificates.GetValueOrDefault(
                            material.RawMaterialId);
                        return new PremiseMenuInformationRow(
                            menu.MenuId,
                            menu.Name,
                            brand,
                            material.RawMaterialId,
                            value.Ingredient,
                            manufacturer,
                            certificate?.ReferenceNo,
                            certificate?.Authority,
                            certificate?.ExpiryDate,
                            certificate?.Status);
                    })
                    .ToList();
                if (rows.Count == 0)
                    rows.Add(new PremiseMenuInformationRow(
                        menu.MenuId, menu.Name, brand, null, null, null, null, null, null, null));
                return rows;
            })
            .OrderBy(row => row.MenuName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Ingredient, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.MenuId)
            .ToList();
    }

    private static async Task<IReadOnlyDictionary<Guid, string>> LoadManufacturerNamesAsync(
        VHSmartDbContext db,
        IReadOnlyCollection<Guid> manufacturerIds,
        CancellationToken ct)
    {
        if (manufacturerIds.Count == 0)
            return new Dictionary<Guid, string>();
        return await db.ManufacturerSuppliers
            .AsNoTracking()
            .Where(row => manufacturerIds.Contains(row.Id))
            .ToDictionaryAsync(
                row => row.Id,
                row => row.ManufacturerName ?? row.SupplierName ?? string.Empty,
                ct);
    }
}

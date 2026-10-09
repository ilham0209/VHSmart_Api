using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Premise.ManagePremise;

// Product Information (Current) / (Approved) tabs of the premise modal (spec 7.7
// [CONFIRMED]: same row structure as the Menu tab - # / Product / Brand / Ingredient
// Information). One flat row per product x ACTIVE ingredient; a product without visible
// ingredients still gets one row with empty ingredient cells.
//
// Current = the company's products (Database.md 9 stores products on the company, never on
// a premise - spec 7.7's legacy "copy products from a factory premise" relation has no
// column in this schema, so the Factory premise tab shows its company's products; flagged).
// Approved = AppCertificateItems rows with a ProductId (the snapshot the approval hook
// writes when an application crosses APPLICATION APPROVED - Database.md 12): one row per
// certificate item, the live product's name/brand/ingredients where it still exists, the
// stored ItemName/BrandId where it does not. "Empty if none" = no items yet.
// Ingredient rows carry the product's ACTIVE links only (the tab has no Mapping Status
// column, and D-18 treats ACTIVE as linked); certificate columns reuse
// RawMaterialHalalInformation (D-04 + Database.md 13).
public record GetPremiseProductInformationQuery(Guid PremiseId, bool Approved)
    : IRequest<IReadOnlyList<PremiseProductInformationRow>>;

public record PremiseProductInformationRow(
    Guid ProductId,
    string ProductName,
    string? Brand,
    Guid? RawMaterialId,
    string? Ingredient,
    string? ManufacturerName,
    string? ReferenceNo,
    string? Authority,
    DateOnly? ExpiryDate,
    HalalStatus? Status);

public class GetPremiseProductInformationHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetPremiseProductInformationQuery, IReadOnlyList<PremiseProductInformationRow>>
{
    public async Task<IReadOnlyList<PremiseProductInformationRow>> Handle(
        GetPremiseProductInformationQuery request,
        CancellationToken ct)
    {
        // Unknown / foreign premise answers 404 before anything else, the stance of every
        // read in this controller.
        var premiseExists = await db.Premises
            .AsNoTracking()
            .AnyAsync(
                row => row.Id == request.PremiseId && row.CompanyId == user.CompanyId, ct);
        if (!premiseExists)
            throw new NotFoundException("Premise not found.");

        List<ProductEntry> entries;
        if (request.Approved)
        {
            // One entry per certificate item (existence of the item = the application was
            // approved; the status may have moved on since). A soft-deleted product keeps
            // its snapshot name and shows no ingredient rows.
            var items = await db.CertificateItems
                .AsNoTracking()
                .Where(row => row.CompanyId == user.CompanyId && row.ProductId != null)
                .Select(row => new
                {
                    row.ProductId,
                    row.ItemName,
                    row.BrandId
                })
                .ToListAsync(ct);
            if (items.Count == 0)
                return [];

            var liveIds = items
                .Select(row => row.ProductId!.Value)
                .Distinct()
                .ToList();
            var liveById = (await db.Products
                    .AsNoTracking()
                    .Where(row => liveIds.Contains(row.Id))
                    .Select(row => new { row.Id, row.Name, row.BrandId })
                    .ToListAsync(ct))
                .ToDictionary(row => row.Id);

            entries = items
                .Select(item =>
                {
                    var live = liveById.GetValueOrDefault(item.ProductId!.Value);
                    return new ProductEntry(
                        item.ProductId!.Value,
                        live?.Name ?? item.ItemName,
                        live?.BrandId ?? item.BrandId,
                        HasLiveProduct: live is not null);
                })
                .ToList();
        }
        else
        {
            entries = await db.Products
                .AsNoTracking()
                .Where(row => row.CompanyId == user.CompanyId)
                .Select(row => new ProductEntry(
                    row.Id,
                    row.Name ?? string.Empty,
                    row.BrandId,
                    HasLiveProduct: true))
                .ToListAsync(ct);
            if (entries.Count == 0)
                return [];
        }

        // Ingredient rows are loaded for live products only: a soft-deleted product keeps
        // its snapshot row with empty ingredient cells instead of resurrecting its links.
        var liveProductIds = entries
            .Where(entry => entry.HasLiveProduct)
            .Select(entry => entry.ProductId)
            .Distinct()
            .ToList();
        var links = new List<(Guid ProductId, Guid RawMaterialId)>();
        if (liveProductIds.Count > 0)
        {
            var linkRows = await db.ProductIngredients
                .AsNoTracking()
                .Where(row => liveProductIds.Contains(row.ProductId)
                    && row.MappingStatus == ProductIngredientMappingStatus.Active)
                .Select(row => new { row.ProductId, row.RawMaterialId })
                .ToListAsync(ct);
            links.AddRange(linkRows.Select(row => (row.ProductId, row.RawMaterialId)));
        }

        var rawMaterialIds = links
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
        var manufacturerIds = rawMaterials
            .Select(row => row.ManufacturerSupplierId)
            .Distinct()
            .ToList();
        var manufacturerById = manufacturerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.ManufacturerSuppliers
                .AsNoTracking()
                .Where(row => manufacturerIds.Contains(row.Id))
                .ToDictionaryAsync(
                    row => row.Id,
                    row => row.ManufacturerName ?? row.SupplierName ?? string.Empty,
                    ct);
        var materialById = rawMaterials.ToDictionary(row => row.Id);

        var brandIds = entries
            .Where(row => row.BrandId != null)
            .Select(row => row.BrandId!.Value)
            .Distinct()
            .ToList();
        var brandById = brandIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.GeneralData
                .AsNoTracking()
                .Where(row => brandIds.Contains(row.Id))
                .ToDictionaryAsync(row => row.Id, row => row.Name, ct);

        return entries
            .SelectMany(entry =>
            {
                var brand = entry.BrandId is null
                    ? null
                    : brandById.GetValueOrDefault(entry.BrandId.Value);
                var rows = links
                    .Where(link => link.ProductId == entry.ProductId
                        && materialById.ContainsKey(link.RawMaterialId))
                    .Select(link =>
                    {
                        var material = materialById[link.RawMaterialId];
                        var manufacturer = manufacturerById.GetValueOrDefault(
                            material.ManufacturerSupplierId);
                        var certificate = certificates.GetValueOrDefault(link.RawMaterialId);
                        return new PremiseProductInformationRow(
                            entry.ProductId,
                            entry.ProductName,
                            brand,
                            link.RawMaterialId,
                            material.Ingredient,
                            manufacturer,
                            certificate?.ReferenceNo,
                            certificate?.Authority,
                            certificate?.ExpiryDate,
                            certificate?.Status);
                    })
                    .ToList();
                if (rows.Count == 0)
                    rows.Add(new PremiseProductInformationRow(
                        entry.ProductId, entry.ProductName, brand,
                        null, null, null, null, null, null, null));
                return rows;
            })
            .OrderBy(row => row.ProductName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Ingredient, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.ProductId)
            .ToList();
    }

    // One entry per response row group: the snapshot fields survive a soft-deleted product,
    // the live flag says whether ingredient rows can be loaded for it.
    private sealed record ProductEntry(
        Guid ProductId,
        string ProductName,
        Guid? BrandId,
        bool HasLiveProduct);
}

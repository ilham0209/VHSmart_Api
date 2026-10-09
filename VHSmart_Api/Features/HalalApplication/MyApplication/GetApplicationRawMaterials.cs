using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.MyApplication;

// Raw Material tab of the application screen (spec 12.5): the distinct raw materials behind
// the batch's ACTIVE product-ingredient links (the Product tab's ingredients, one row per
// material with the products that use it). Rows read through RawMaterials so CodingRules 7.3
// visibility applies, and the halal columns come from the material's HALAL CERTIFICATE
// attachment exactly like Raw Material > Master List's Halal Information column (Database.md
// 10 "derived from the HALAL CERTIFICATE attachment"). A batch-less application has no
// materials.
public record GetApplicationRawMaterialsQuery(Guid Id)
    : IRequest<IReadOnlyList<ApplicationRawMaterialResponse>>;

public record ApplicationRawMaterialResponse(
    Guid RawMaterialId,
    string Ingredient,
    string? IngredientSource,
    string? CommercialName,
    string? ScientificName,
    string? ManufacturerName,
    string? ManufacturerAddress,
    string Products,
    string? DocumentReference,
    string? HalalCertificateProvider,
    DateOnly? ExpiredDate);

public class GetApplicationRawMaterialsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetApplicationRawMaterialsQuery,
        IReadOnlyList<ApplicationRawMaterialResponse>>
{
    public async Task<IReadOnlyList<ApplicationRawMaterialResponse>> Handle(
        GetApplicationRawMaterialsQuery request,
        CancellationToken ct)
    {
        var application = await ApplicationData.FindAsync(db, user, request.Id, ct);
        var batchId = application.BatchId;
        if (batchId is null)
            return [];

        var productIds = await db.BatchProducts.AsNoTracking()
            .Where(row => row.BatchId == batchId
                && row.MappingStatus == BatchProductMappingStatus.Active)
            .Select(row => row.ProductId)
            .ToListAsync(ct);
        if (productIds.Count == 0)
            return [];

        var links = await (
                from link in db.ProductIngredients.AsNoTracking()
                where productIds.Contains(link.ProductId)
                    && link.MappingStatus == ProductIngredientMappingStatus.Active
                select new { link.RawMaterialId, link.ProductId })
            .ToListAsync(ct);
        if (links.Count == 0)
            return [];

        var rawMaterialIds = links
            .Select(link => link.RawMaterialId)
            .Distinct()
            .ToList();

        // The visibility filter of RawMaterials (owner OR Accessible For) decides which of the
        // linked materials this company may see - a link alone never exposes a foreign row.
        var materials = await db.RawMaterials.AsNoTracking()
            .Where(row => rawMaterialIds.Contains(row.Id))
            .Select(row => new
            {
                row.Id,
                row.Ingredient,
                row.IngredientSourceId,
                row.CommercialName,
                row.ScientificName,
                row.ManufacturerSupplierId
            })
            .ToListAsync(ct);
        if (materials.Count == 0)
            return [];

        var visibleIds = materials.Select(row => row.Id).ToHashSet();

        var halalInformation = await RawMaterialHalalInformation.LoadManyAsync(
            db, [.. visibleIds], ct);

        // The source and manufacturer rows are pre-loaded once (the subquery style of the
        // master list would run one round-trip per material here).
        var sourceIds = materials
            .Select(row => row.IngredientSourceId)
            .Where(id => id != null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var sources = await db.GeneralData.AsNoTracking()
            .Where(row => sourceIds.Contains(row.Id))
            .Select(row => new { row.Id, row.Name })
            .ToListAsync(ct);
        var sourceNameById = sources.ToDictionary(row => row.Id, row => row.Name);

        var manufacturerIds = materials
            .Select(row => row.ManufacturerSupplierId)
            .Distinct()
            .ToList();
        var manufacturers = await db.ManufacturerSuppliers.AsNoTracking()
            .Where(row => manufacturerIds.Contains(row.Id))
            .Select(row => new { row.Id, row.ManufacturerName, row.ManufacturerAddress })
            .ToListAsync(ct);
        var manufacturerById = manufacturers.ToDictionary(row => row.Id);

        var productNames = await db.Products.AsNoTracking()
            .Where(row => productIds.Contains(row.Id))
            .Select(row => new { row.Id, row.Name })
            .ToListAsync(ct);
        var productNameById = productNames.ToDictionary(
            row => row.Id,
            row => row.Name ?? string.Empty);

        return materials
            .OrderBy(row => row.Ingredient, StringComparer.OrdinalIgnoreCase)
            .Select(row =>
            {
                var usedBy = links
                    .Where(link => link.RawMaterialId == row.Id)
                    .Select(link => productNameById.GetValueOrDefault(link.ProductId, string.Empty))
                    .Where(name => name.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
                var halalInfo = halalInformation.GetValueOrDefault(row.Id);
                var manufacturer = manufacturerById.GetValueOrDefault(row.ManufacturerSupplierId);

                return new ApplicationRawMaterialResponse(
                    row.Id,
                    row.Ingredient ?? row.CommercialName ?? string.Empty,
                    row.IngredientSourceId is null
                        ? null
                        : sourceNameById.GetValueOrDefault(row.IngredientSourceId.Value),
                    row.CommercialName,
                    row.ScientificName,
                    manufacturer?.ManufacturerName,
                    manufacturer?.ManufacturerAddress,
                    string.Join(", ", usedBy),
                    halalInfo?.ReferenceNo,
                    halalInfo?.Authority,
                    halalInfo?.ExpiryDate);
            })
            .ToList();
    }
}

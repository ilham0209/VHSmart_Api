using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.MyApplication;

// Product tab of the application screen (spec 12.5): the batch's ACTIVE products (Batch >
// Product Management owns the links), each with its brand and its linked ingredients -
// exactly the two extra columns the spec's Product grid shows. INACTIVE links are hidden
// (same stance as Batch > Product Management's list). A batch-less application has no
// products.
public record GetApplicationProductsQuery(Guid Id)
    : IRequest<IReadOnlyList<ApplicationProductResponse>>;

public record ApplicationProductResponse(
    Guid ProductId,
    string ProductName,
    string Brand,
    string Ingredients);

public class GetApplicationProductsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetApplicationProductsQuery, IReadOnlyList<ApplicationProductResponse>>
{
    public async Task<IReadOnlyList<ApplicationProductResponse>> Handle(
        GetApplicationProductsQuery request,
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

        var products = await db.Products.AsNoTracking()
            .Where(row => productIds.Contains(row.Id))
            .Select(row => new
            {
                row.Id,
                row.Name,
                Brand = db.GeneralData
                    .Where(data => data.Id == row.BrandId)
                    .Select(data => data.Name)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var ingredients = await (
                from link in db.ProductIngredients.AsNoTracking()
                where productIds.Contains(link.ProductId)
                    && link.MappingStatus == ProductIngredientMappingStatus.Active
                join rawMaterial in db.RawMaterials.AsNoTracking()
                    on link.RawMaterialId equals rawMaterial.Id
                select new
                {
                    link.ProductId,
                    Name = rawMaterial.Ingredient ?? rawMaterial.CommercialName ?? string.Empty
                })
            .ToListAsync(ct);

        var namesByProduct = ingredients
            .GroupBy(row => row.ProductId)
            .ToDictionary(
                group => group.Key,
                group => string.Join(", ", group
                    .Select(row => row.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)));

        return products
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .Select(row => new ApplicationProductResponse(
                row.Id,
                row.Name ?? string.Empty,
                row.Brand ?? string.Empty,
                namesByProduct.TryGetValue(row.Id, out var names) ? names : string.Empty))
            .ToList();
    }
}

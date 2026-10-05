using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageProduct;

// Tab "Manage Ingredient Information" of the product form (spec 9.1, [CONFIRMED] columns):
// Action (link / unlink) is client side - it calls Link / Unlink - and the rest of the row is
// joined display data. The tab is only readable when the company has at least one brand linked
// (spec 6.3 / 9.1, enforced by ProductIngredientData), and a raw material the caller cannot see
// under the 7.3 filter simply does not render. Not a DataGrid: the spec shows no Search box and
// no "Show N entries" on this table, like the other detail tabs of this codebase.
public record GetProductIngredientsQuery(Guid ProductId)
    : IRequest<IReadOnlyList<ProductIngredientResponse>>;

// "Manufacturer Information (name, address, contact)" of the ingredient row: the raw material's
// manufacturer, falling back to its supplier half so a row that only knows a supplier is not
// empty - the same stance the product list's Manufacturer column takes.
public record ProductIngredientManufacturerInformation(
    string? Name,
    string? Address,
    string? Contact);

// "Halal Certificate Information" of the ingredient row. Status is null when the raw material
// has no HALAL CERTIFICATE at all - the spec renders that cell as "Not Available".
public record ProductIngredientHalalCertificateInformation(
    string? ReferenceNo,
    string? Authority,
    DateOnly? ExpiryDate,
    HalalStatus? Status);

public record ProductIngredientResponse(
    Guid Id,
    Guid RawMaterialId,
    string? Ingredient,
    ProductIngredientManufacturerInformation ManufacturerInformation,
    ProductIngredientHalalCertificateInformation HalalCertificateInformation,
    string MappingStatus,
    DateTime? ModifiedDate);

public class GetProductIngredientsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetProductIngredientsQuery, IReadOnlyList<ProductIngredientResponse>>
{
    public async Task<IReadOnlyList<ProductIngredientResponse>> Handle(
        GetProductIngredientsQuery request,
        CancellationToken ct) =>
        await ProductIngredientListLoader.LoadAsync(db, user, request.ProductId, ct);
}

// One code path for the GET and for the list a link returns (the same reasoning as the other
// detail loaders of this codebase): product 404 first, then the brand gate, then the rows.
internal static class ProductIngredientListLoader
{
    public static async Task<IReadOnlyList<ProductIngredientResponse>> LoadAsync(
        VHSmartDbContext db,
        ICurrentUser user,
        Guid productId,
        CancellationToken ct)
    {
        // The explicit CompanyId match keeps a Switch Company = ALL caller on their own rows,
        // exactly like the product detail (an unknown or foreign id answers 404, never 403).
        var productExists = await db.Products
            .AsNoTracking()
            .AnyAsync(row => row.Id == productId && row.CompanyId == user.CompanyId, ct);
        if (!productExists)
            throw new NotFoundException("Product not found.");

        await ProductIngredientData.EnsureBrandLinkedAsync(db, user, ct);

        var links = await db.ProductIngredients
            .AsNoTracking()
            .Where(row => row.ProductId == productId)
            .ToListAsync(ct);

        var rawMaterialIds = links.Select(row => row.RawMaterialId).Distinct().ToList();
        var rawMaterials = await db.RawMaterials
            .AsNoTracking()
            .Where(row => rawMaterialIds.Contains(row.Id))
            .Select(row => new { row.Id, row.Ingredient, row.ManufacturerSupplierId })
            .ToListAsync(ct);

        var certificates = await RawMaterialHalalInformation.LoadManyAsync(db, rawMaterialIds, ct);

        var manufacturerIds = rawMaterials
            .Select(row => row.ManufacturerSupplierId)
            .Distinct()
            .ToList();
        var manufacturers = await db.ManufacturerSuppliers
            .AsNoTracking()
            .Where(row => manufacturerIds.Contains(row.Id))
            .Select(row => new
            {
                row.Id,
                Name = row.ManufacturerName ?? row.SupplierName,
                Address = row.ManufacturerAddress ?? row.SupplierAddress,
                Contact = row.ManufacturerContactNo ?? row.SupplierContactNo
            })
            .ToListAsync(ct);
        var manufacturerById = manufacturers.ToDictionary(row => row.Id);

        var materialById = rawMaterials.ToDictionary(row => row.Id);

        // A raw material that was deleted or unshared after the link drops out of the list
        // (the 7.3 filter hides it) - there is nothing left to show for that row.
        return links
            .Where(link => materialById.ContainsKey(link.RawMaterialId))
            .Select(link =>
            {
                var material = materialById[link.RawMaterialId];
                var manufacturer = manufacturerById.GetValueOrDefault(material.ManufacturerSupplierId);
                var certificate = certificates.GetValueOrDefault(link.RawMaterialId);

                return new ProductIngredientResponse(
                    link.Id,
                    link.RawMaterialId,
                    material.Ingredient,
                    new ProductIngredientManufacturerInformation(
                        manufacturer?.Name,
                        manufacturer?.Address,
                        manufacturer?.Contact),
                    new ProductIngredientHalalCertificateInformation(
                        certificate?.ReferenceNo,
                        certificate?.Authority,
                        certificate?.ExpiryDate,
                        certificate?.Status),
                    link.MappingStatus,
                    link.SysDateModified);
            })
            .OrderBy(row => row.Ingredient, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Id)
            .ToList();
    }
}

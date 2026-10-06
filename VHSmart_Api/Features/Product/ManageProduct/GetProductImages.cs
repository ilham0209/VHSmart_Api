using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageProduct;

// Tab "Manage Attachment Information" (spec 9.1): the product images table - Action, Image,
// Document Type, Document Name, Version. Only the CURRENT row of each of the four angles is
// listed (Database.md 9: a replacement keeps the old row with IsCurrent = false and adds a new
// one), so the table never shows more than four rows, ordered front / back / left / right the
// way the guidelines box lists the angles. It is a tab table, not a DataGrid: the spec shows no
// Search box and there are at most four rows. An unknown or foreign product answers 404.
public record GetProductImagesQuery(Guid ProductId)
    : IRequest<IReadOnlyList<ProductImageResponse>>;

public record ProductImageResponse(
    Guid Id,
    ProductImagePosition Position,
    string DocumentType,
    string DocumentName,
    int Version);

public class GetProductImagesHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetProductImagesQuery, IReadOnlyList<ProductImageResponse>>
{
    public async Task<IReadOnlyList<ProductImageResponse>> Handle(
        GetProductImagesQuery request,
        CancellationToken ct) =>
        await ProductImageListLoader.LoadAsync(db, user, request.ProductId, ct);
}

// One code path for GET and for the list returned after an upload or a delete (the same
// reasoning as the other detail loaders of this codebase).
internal static class ProductImageListLoader
{
    public static async Task<IReadOnlyList<ProductImageResponse>> LoadAsync(
        VHSmartDbContext db,
        ICurrentUser user,
        Guid productId,
        CancellationToken ct)
    {
        // An explicit CompanyId match (PD-01 stance) is what makes an unknown, foreign or
        // soft-deleted product 404 instead of returning an empty table.
        var productExists = await db.Products.AsNoTracking()
            .AnyAsync(row => row.Id == productId && row.CompanyId == user.CompanyId, ct);
        if (!productExists)
            throw new NotFoundException("Product not found.");

        var current = await db.ProductImages.AsNoTracking()
            .Where(row => row.ProductId == productId && row.IsCurrent)
            .ToListAsync(ct);

        return current
            .OrderBy(row => ProductImageData.Order(row.Position))
            .Select(row => new ProductImageResponse(
                row.Id,
                row.Position,
                ProductImageData.DocumentType(row.Position),
                row.Image.FileName,
                row.Version))
            .ToList();
    }
}

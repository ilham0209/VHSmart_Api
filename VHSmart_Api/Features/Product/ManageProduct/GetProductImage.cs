using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Product.ManageProduct;

// The tab's Image cell / "view" half of the Action icon (spec 9.1): the stored picture of one
// angle, streamed inline. Same rules as every download in this codebase (CodingRules 10): the
// permission of viewing the record, 404 for an unknown, foreign or mismatched id, 404 when the
// stored bytes are gone.
public record GetProductImageQuery(Guid ProductId, Guid ImageId)
    : IRequest<ProductImageContentResponse>;

// Named apart from ProductImageResponse (the row of the tab table) so the two never collide.
public record ProductImageContentResponse(Stream Content, string ContentType);

public class GetProductImageHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage)
    : IRequestHandler<GetProductImageQuery, ProductImageContentResponse>
{
    public async Task<ProductImageContentResponse> Handle(
        GetProductImageQuery request,
        CancellationToken ct)
    {
        await ProductImageData.GetProductAsync(db, user, request.ProductId, ct);

        var image = await db.ProductImages
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.ImageId
                    && row.ProductId == request.ProductId
                    && row.IsCurrent,
                ct);
        if (image is null)
            throw new NotFoundException("Image not found.");

        if (string.IsNullOrWhiteSpace(image.Image.StorageKey))
            throw new NotFoundException("No image.");

        var content = await storage.OpenReadAsync(image.Image.StorageKey, ct);
        var contentType = string.IsNullOrWhiteSpace(image.Image.ContentType)
            ? "application/octet-stream"
            : image.Image.ContentType;

        return new ProductImageContentResponse(content, contentType);
    }
}

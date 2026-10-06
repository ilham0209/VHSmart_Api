using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageProduct;

// Tab "Manage Attachment Information", Action > delete (spec 9.1): soft delete of the row the
// table shows (CodingRules 7.1 - no physical DELETE anywhere in this system). The angle becomes
// empty again; a later upload starts at the next version number, because the number of a
// deleted row is never reused (see UploadProductImage). Superseded rows are history and are
// never listed, so only the current row can be reached through the API.
public record DeleteProductImageCommand(Guid ProductId, Guid ImageId) : IRequest;

public class DeleteProductImageHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteProductImageCommand>
{
    public async Task Handle(DeleteProductImageCommand request, CancellationToken ct)
    {
        await ProductImageData.GetProductAsync(db, user, request.ProductId, ct);

        var entity = await db.ProductImages
            .FirstOrDefaultAsync(
                row => row.Id == request.ImageId
                    && row.ProductId == request.ProductId
                    && row.IsCurrent,
                ct);

        if (entity is null)
            throw new NotFoundException("Image not found.");

        db.ProductImages.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}

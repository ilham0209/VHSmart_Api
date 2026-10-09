using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageProduct;

// Remove a product (spec 9.1 list, Action > delete): soft delete (CodingRules 7.1). The
// "delete only when unused" guard this file deferred to HA-01 (PD-01 comment, PD-03 flag)
// now covers the referencing tables that exist in the model - PrdProductIngredients (PD-02),
// PrdProductImages (PD-03) and AppBatchProducts (HA-01). AppCertificateItems (HA) does not
// exist yet, so its part of the guard lands with that task, exactly like RM-01 left the
// PrdProducts and AppBatches parts to land here. The explicit CompanyId match keeps a Switch
// Company = ALL caller on their own rows.
public record DeleteProductCommand(Guid Id) : IRequest;

public class DeleteProductHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteProductCommand>
{
    public async Task Handle(DeleteProductCommand request, CancellationToken ct)
    {
        var entity = await db.Products
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Product not found.");

        await ProductDeletionGuard.EnsureNotInUseAsync(db, entity.Id, ct);

        db.Products.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}

// The in-use checks of both delete paths (single and "Multiple Delete"), so one product can
// never be freed through the toolbar that the row delete refuses. Referenced rows are checked
// with the filtered set (CodingRules 7.2): only LIVE rows block - a soft-deleted link does
// not. No spec message exists for this rule, so the texts are ours (the RM-01 stance).
internal static class ProductDeletionGuard
{
    public static async Task EnsureNotInUseAsync(
        VHSmartDbContext db,
        Guid productId,
        CancellationToken ct)
    {
        if (await db.BatchProducts.AnyAsync(row => row.ProductId == productId, ct))
            throw new BusinessRuleException("This product is used by a batch.");

        if (await db.ProductIngredients.AnyAsync(row => row.ProductId == productId, ct))
            throw new BusinessRuleException("This product is used by an ingredient link.");

        if (await db.ProductImages.AnyAsync(row => row.ProductId == productId, ct))
            throw new BusinessRuleException("This product is used by a product image.");
    }
}

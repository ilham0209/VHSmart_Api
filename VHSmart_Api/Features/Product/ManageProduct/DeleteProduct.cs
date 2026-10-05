using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageProduct;

// Remove a product (spec 9.1 list, Action > delete): soft delete (CodingRules 7.1). There
// is no "delete only when unused" guard yet: PrdProductIngredients (PD-02),
// PrdProductImages (PD-03), AppBatchProducts (HA-01) and AppCertificateItems (HA) all
// reference this table and none of them exists in the model yet - exactly like the raw
// material delete of RM-02 before its referencing tables landed. The explicit CompanyId
// match keeps a Switch Company = ALL caller on their own rows.
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

        db.Products.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}

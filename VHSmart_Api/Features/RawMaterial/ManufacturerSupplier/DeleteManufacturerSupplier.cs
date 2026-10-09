using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;

// Remove a Manufacturer & Supplier (spec 10.1 list: delete action): soft delete (CodingRules
// 7.1). The "delete only when unused" guard now covers RawMaterials (RM-01), PrdProducts
// (PD-01) and AppBatches (HA-01) - the two parts RM-01 deferred to the tasks that own those
// tables; AppCertificateItems (HA) does not exist yet, so its part lands there, like the
// Service Provider guard that belongs to PY-02. The tenant filter makes another company's
// row (or an already deleted one) answer 404 instead of 204.
public record DeleteManufacturerSupplierCommand(Guid Id) : IRequest;

public class DeleteManufacturerSupplierHandler(VHSmartDbContext db)
    : IRequestHandler<DeleteManufacturerSupplierCommand>
{
    public async Task Handle(DeleteManufacturerSupplierCommand request, CancellationToken ct)
    {
        var entity = await db.ManufacturerSuppliers
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Manufacturer and supplier not found.");

        // Referenced rows are checked with the filtered set (CodingRules 7.2): only live rows
        // of the tenant set block the delete. No spec message exists for this rule, so the
        // texts are ours.
        if (await db.RawMaterials.AnyAsync(row => row.ManufacturerSupplierId == entity.Id, ct))
            throw new BusinessRuleException(
                "This manufacturer and supplier is used by a raw material.");

        if (await db.Products.AnyAsync(row => row.ManufacturerSupplierId == entity.Id, ct))
            throw new BusinessRuleException(
                "This manufacturer and supplier is used by a product.");

        if (await db.Batches.AnyAsync(row => row.ManufacturerSupplierId == entity.Id, ct))
            throw new BusinessRuleException(
                "This manufacturer and supplier is used by a batch.");

        db.ManufacturerSuppliers.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}

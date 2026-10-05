using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;

// Remove a Manufacturer & Supplier (spec 10.1 list: delete action): soft delete (CodingRules
// 7.1). The "delete only when unused" guard cannot be checked yet - RawMaterials (RM-02),
// PrdProducts (PD-01) and AppBatches (HA-01) all reference this table and none of them exists
// in the model, exactly like the Service Provider guard that belongs to PY-02. The tenant
// filter makes another company's row (or an already deleted one) answer 404 instead of 204.
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

        db.ManufacturerSuppliers.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}

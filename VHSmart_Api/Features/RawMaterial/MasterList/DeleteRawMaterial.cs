using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.RawMaterial.MasterList;

// Remove a raw material (spec 10.2 list, Action > delete): soft delete (CodingRules 7.1). The
// "Accessible For" rows stay with the row, like every other child list in this codebase. There
// is no "delete only when unused" guard yet: PrdProductIngredients (PD-04), PrdMenuRawMaterials
// (PD-06) and RawMaterialAttachments (RM-03) all reference this table and none of them exists in
// the model yet - exactly like the Manufacturer & Supplier guard this task added in RM-01.
public record DeleteRawMaterialCommand(Guid Id) : IRequest;

public class DeleteRawMaterialHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteRawMaterialCommand>
{
    public async Task Handle(DeleteRawMaterialCommand request, CancellationToken ct)
    {
        var entity = await db.RawMaterials
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Raw material not found.");

        RawMaterialData.EnsureOwner(entity, user);

        db.RawMaterials.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}

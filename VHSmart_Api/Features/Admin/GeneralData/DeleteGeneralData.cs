using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Admin.GeneralData;

// Remove a dropdown value (spec 5.1, trash action): soft delete - the unique slot frees up
// again for a new row with the same name. The tenant filter makes another company's row (or an
// already deleted one) answer 404 instead of 204 (CodingRules 9).
public record DeleteGeneralDataCommand(Guid Id) : IRequest;

public class DeleteGeneralDataHandler(VHSmartDbContext db)
    : IRequestHandler<DeleteGeneralDataCommand>
{
    public async Task Handle(DeleteGeneralDataCommand request, CancellationToken ct)
    {
        var entity = await db.GeneralData
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("General data not found.");

        db.GeneralData.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}

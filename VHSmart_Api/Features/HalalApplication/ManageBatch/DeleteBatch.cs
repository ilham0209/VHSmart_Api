using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.ManageBatch;

// Remove a batch (spec 12.2 list, Action > delete): soft delete (CodingRules 7.1). The
// spec's "locking or deleting a batch once used by an application" is [VERIFY] with no
// Decisions default, and AppHalalApplications does not exist in the model yet (HA-02 builds
// it), so there is no referencing row to guard on - exactly like DeleteProduct before its
// referencing tables landed. The product / premise links stay with the batch, like every
// other child list of this codebase. The explicit CompanyId match keeps a Switch Company =
// ALL caller on their own rows.
public record DeleteBatchCommand(Guid Id) : IRequest;

public class DeleteBatchHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteBatchCommand>
{
    public async Task Handle(DeleteBatchCommand request, CancellationToken ct)
    {
        var entity = await db.Batches
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Batch not found.");

        db.Batches.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}

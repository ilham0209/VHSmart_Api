using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Audit.AuditPrefix;

// Remove a prefix row (spec 14.2 trash action): soft delete - the brand's slot frees up
// again for a new row. Deleting never breaks an already issued Audit Reference No. (those
// are stored on the plan); a future planning run on a brand without a live prefix is
// rejected by D-09 with "Audit Prefix is not set for brand {name}". The tenant filter makes
// another company's row (or an already deleted one) answer 404 instead of 204 (CodingRules 9).
public record DeleteAuditPrefixCommand(Guid Id) : IRequest;

public class DeleteAuditPrefixHandler(VHSmartDbContext db)
    : IRequestHandler<DeleteAuditPrefixCommand>
{
    public async Task Handle(DeleteAuditPrefixCommand request, CancellationToken ct)
    {
        var entity = await db.AuditPrefixes
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Audit prefix not found.");

        db.AuditPrefixes.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}

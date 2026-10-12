using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.GroupAuditor;

// Delete a group (spec 14.7 trash action): children (member rows) are soft-deleted first,
// then the group - the AU-05 children-first precedent. Unknown or foreign row -> 404
// before any write. NO lock: spec 14.6 freezes checklists once used by a plan but says
// nothing about groups; a plan's GroupAuditorId soft-deleted group persists as a row, so
// the Restrict FK is never violated by a soft delete (flagged in the report). No
// notification: groups are not "business master" data (spec 9.x).
public record DeleteGroupAuditorCommand(Guid Id) : IRequest;

public class DeleteGroupAuditorHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteGroupAuditorCommand>
{
    public async Task Handle(DeleteGroupAuditorCommand request, CancellationToken ct)
    {
        var entity = await db.GroupAuditors.FirstOrDefaultAsync(
            row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Group auditor not found.");

        var members = await db.GroupAuditorMembers
            .Where(row => row.GroupAuditorId == entity.Id)
            .ToListAsync(ct);
        foreach (var member in members)
            db.GroupAuditorMembers.Remove(member);

        db.GroupAuditors.Remove(entity);

        await db.SaveChangesAsync(ct);
    }
}

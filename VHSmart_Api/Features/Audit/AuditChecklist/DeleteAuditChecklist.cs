using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditChecklist;

// Remove a checklist (spec 14.6 trash action): soft delete the row AND its Criteria
// Selection links - the links belong to the checklist and would otherwise orphan (the
// AU-03/AU-04 children-first order; the FK is non-nullable + Restrict). The computed lock
// of spec 14.6 ([MANUAL], Database.md 12) applies here too: a checklist referenced by any
// live AudAuditPlans row answers 409 (ours - flagged), checked after the 404 lookup so an
// unknown/foreign row still reads 404 (CodingRules 9). NO notification: spec 21.8 names
// one delete notification in this domain - Finding only.
public record DeleteAuditChecklistCommand(Guid Id) : IRequest;

public class DeleteAuditChecklistHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteAuditChecklistCommand>
{
    public async Task Handle(DeleteAuditChecklistCommand request, CancellationToken ct)
    {
        var entity = await db.AuditChecklists.FirstOrDefaultAsync(
            row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Audit checklist not found.");

        var locked = await db.AuditPlans.AnyAsync(
            plan => plan.ChecklistId == entity.Id && plan.CompanyId == user.CompanyId, ct);
        if (locked)
            throw new ConflictException(
                "This checklist is used by an audit plan and cannot be deleted.");

        var links = await db.AuditChecklistCriteria
            .Where(link => link.ChecklistId == entity.Id)
            .ToListAsync(ct);

        foreach (var link in links)
            db.AuditChecklistCriteria.Remove(link);
        db.AuditChecklists.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}

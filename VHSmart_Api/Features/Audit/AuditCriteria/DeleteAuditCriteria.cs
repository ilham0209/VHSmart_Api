using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Audit.AuditCriteria;

// Remove a criteria row (spec 14.5 trash action): soft delete the row AND its Finding
// Selection links - the links belong to the criteria and would otherwise orphan (the AU-03
// stance; children removed before the parent per the Users/DeleteUser order because the
// FK is non-nullable + Restrict). Unknown or foreign row -> 404 (CodingRules 9). NO guard
// against rows a checklist already uses: the spec states no such rule for criteria (the
// lock belongs to checklists - 14.6 / AU-05), so a template silently loses the row on its
// next read - flagged. NO notification: spec 21.8 names one only for Finding delete.
public record DeleteAuditCriteriaCommand(Guid Id) : IRequest;

public class DeleteAuditCriteriaHandler(VHSmartDbContext db)
    : IRequestHandler<DeleteAuditCriteriaCommand>
{
    public async Task Handle(DeleteAuditCriteriaCommand request, CancellationToken ct)
    {
        var entity = await db.AuditCriteria
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Audit criteria not found.");

        var links = await db.AuditCriteriaFindings
            .Where(link => link.AuditCriteriaId == entity.Id)
            .ToListAsync(ct);

        foreach (var link in links)
            db.AuditCriteriaFindings.Remove(link);
        db.AuditCriteria.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}

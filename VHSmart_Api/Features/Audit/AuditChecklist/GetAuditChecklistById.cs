using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditChecklist;

// The view/edit modal of one checklist (spec 14.6 row actions): the header fields plus the
// live Criteria Selection ids so the table renders its current ticks. An unknown or foreign
// (or soft-deleted) row answers 404 - the client must not learn that the id exists elsewhere
// (CodingRules 9). The explicit CompanyId match keeps a Switch Company = ALL caller on their
// own checklists (spec 14.0). A locked checklist still reads here (view is allowed, only
// edit and delete 409); the response carries no lock flag because the spec shows no such
// column - the client learns from the 409 on save (flagged).
public record GetAuditChecklistByIdQuery(Guid Id) : IRequest<AuditChecklistResponse>;

public class GetAuditChecklistByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAuditChecklistByIdQuery, AuditChecklistResponse>
{
    public async Task<AuditChecklistResponse> Handle(
        GetAuditChecklistByIdQuery request,
        CancellationToken ct)
    {
        var row = await db.AuditChecklists
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == request.Id
                    && candidate.CompanyId == user.CompanyId,
                ct);

        if (row is null)
            throw new NotFoundException("Audit checklist not found.");

        var criteriaIds = await db.AuditChecklistCriteria
            .AsNoTracking()
            .Where(link => link.ChecklistId == row.Id)
            .OrderBy(link => link.AuditCriteriaId)
            .Select(link => link.AuditCriteriaId)
            .ToListAsync(ct);

        return CreateAuditChecklistHandler.ToResponse(row, criteriaIds);
    }
}

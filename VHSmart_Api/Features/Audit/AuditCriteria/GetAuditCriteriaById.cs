using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditCriteria;

// The view/edit modal of one criteria row (spec 14.5 row actions): the modal fields plus
// the linked finding ids so the Finding Selection can render its state. An unknown or
// foreign (or soft-deleted) row answers 404 - the client must not learn that the id exists
// elsewhere (CodingRules 9). The explicit CompanyId match keeps a Switch Company = ALL
// caller on their own rows (spec 14.0).
public record GetAuditCriteriaByIdQuery(Guid Id) : IRequest<AuditCriteriaResponse>;

public class GetAuditCriteriaByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAuditCriteriaByIdQuery, AuditCriteriaResponse>
{
    public async Task<AuditCriteriaResponse> Handle(
        GetAuditCriteriaByIdQuery request,
        CancellationToken ct)
    {
        var row = await db.AuditCriteria
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == request.Id
                    && candidate.CompanyId == user.CompanyId,
                ct);

        if (row is null)
            throw new NotFoundException("Audit criteria not found.");

        var findingIds = await db.AuditCriteriaFindings
            .AsNoTracking()
            .Where(link => link.AuditCriteriaId == row.Id)
            .OrderBy(link => link.FindingId)
            .Select(link => link.FindingId)
            .ToListAsync(ct);

        return CreateAuditCriteriaHandler.ToResponse(row, findingIds);
    }
}

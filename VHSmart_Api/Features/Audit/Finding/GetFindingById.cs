using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.Finding;

// The view/edit modal of one finding (spec 14.4 row actions): the modal fields plus the
// linked recommendation ids so the checkbox table can render its state. An unknown or
// foreign (or soft-deleted) row answers 404 - the client must not learn that the id exists
// elsewhere (CodingRules 9). The explicit CompanyId match keeps a Switch Company = ALL
// caller on their own rows (spec 14.0).
public record GetFindingByIdQuery(Guid Id) : IRequest<GetFindingByIdResponse>;

public record GetFindingByIdResponse(
    Guid Id,
    string Name,
    string FindingCode,
    string? Description,
    DateTime? ModifiedDate,
    IReadOnlyList<Guid> RecommendationIds);

public class GetFindingByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetFindingByIdQuery, GetFindingByIdResponse>
{
    public async Task<GetFindingByIdResponse> Handle(
        GetFindingByIdQuery request,
        CancellationToken ct)
    {
        var row = await db.Findings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == request.Id
                    && candidate.CompanyId == user.CompanyId,
                ct);

        if (row is null)
            throw new NotFoundException("Finding not found.");

        // Live links only: a row unlinked by an edit was soft-deleted and is invisible.
        var recommendationIds = await db.FindingRecommendations
            .AsNoTracking()
            .Where(link => link.FindingId == row.Id)
            .OrderBy(link => link.RecommendationId)
            .Select(link => link.RecommendationId)
            .ToListAsync(ct);

        return new GetFindingByIdResponse(
            row.Id,
            row.Name,
            row.FindingCode,
            row.Description,
            row.SysDateModified,
            recommendationIds);
    }
}

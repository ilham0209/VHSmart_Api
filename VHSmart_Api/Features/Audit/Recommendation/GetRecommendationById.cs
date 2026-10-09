using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Audit.Recommendation;

// The view/edit modal of one recommendation (spec 14.3 row actions). An unknown or foreign
// (or soft-deleted) row answers 404 - the client must not learn that the id exists elsewhere
// (CodingRules 9).
public record GetRecommendationByIdQuery(Guid Id) : IRequest<GetRecommendationByIdResponse>;

public record GetRecommendationByIdResponse(
    Guid Id,
    string Name,
    string RecommendationCode,
    string? Description,
    DateTime? ModifiedDate);

public class GetRecommendationByIdHandler(VHSmartDbContext db)
    : IRequestHandler<GetRecommendationByIdQuery, GetRecommendationByIdResponse>
{
    public async Task<GetRecommendationByIdResponse> Handle(
        GetRecommendationByIdQuery request,
        CancellationToken ct)
    {
        var row = await db.Recommendations
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == request.Id, ct);

        if (row is null)
            throw new NotFoundException("Recommendation not found.");

        return new GetRecommendationByIdResponse(
            row.Id, row.Name, row.RecommendationCode, row.Description, row.SysDateModified);
    }
}

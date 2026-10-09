using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Audit.Recommendation;

// Remove a recommendation (spec 14.3 trash action): soft delete - no physical DELETE ever
// (CodingRules 7.1). Whether a recommendation already linked to a Finding may be deleted is
// not known (spec 14.4 [VERIFY]) - the link table keeps its rows and AU-03 decides the
// behaviour later; this screen only hides the row. The tenant filter makes another company's
// row (or an already deleted one) answer 404 instead of 204 (CodingRules 9).
public record DeleteRecommendationCommand(Guid Id) : IRequest;

public class DeleteRecommendationHandler(VHSmartDbContext db)
    : IRequestHandler<DeleteRecommendationCommand>
{
    public async Task Handle(DeleteRecommendationCommand request, CancellationToken ct)
    {
        var entity = await db.Recommendations
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Recommendation not found.");

        db.Recommendations.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}

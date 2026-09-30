using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Admin.Lookups;

// Scheme dropdown of the picker in spec 12.1 (also used by Halal Policy, Product and Batch).
// Sorted by SortOrder so the picker shows the 9 schemes in the spec's order (R-01).
public record GetSchemesQuery : IRequest<IReadOnlyList<GetSchemesResponse>>;

public record GetSchemesResponse(
    Guid Id,
    string? Code,
    string Name,
    bool IsFoodPremise,
    int SortOrder);

public class GetSchemesHandler(VHSmartDbContext db)
    : IRequestHandler<GetSchemesQuery, IReadOnlyList<GetSchemesResponse>>
{
    public async Task<IReadOnlyList<GetSchemesResponse>> Handle(
        GetSchemesQuery request,
        CancellationToken ct) =>
        await db.Schemes
            .AsNoTracking()
            .OrderBy(scheme => scheme.SortOrder)
            .ThenBy(scheme => scheme.Name)
            .Select(scheme => new GetSchemesResponse(
                scheme.Id, scheme.Code, scheme.Name, scheme.IsFoodPremise, scheme.SortOrder))
            .ToListAsync(ct);
}

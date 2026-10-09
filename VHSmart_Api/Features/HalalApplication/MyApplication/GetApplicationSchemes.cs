using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.HalalApplication.MyApplication;

// Scheme picker of "+ New Application" (spec 12.3 step 2): the nine seeded schemes (12.1,
// Database.md 14) in picker order; "Proceed" enables only after one is selected (client
// side). Code rides along because the D-09 reference number shows it as ({SchemeCode}) and
// the list screen may render it. Global [G] rows, so no tenant scope applies.
public record GetApplicationSchemesQuery : IRequest<IReadOnlyList<ApplicationSchemeOptionResponse>>;

public record ApplicationSchemeOptionResponse(Guid Id, string? Code, string Name);

public class GetApplicationSchemesHandler(VHSmartDbContext db)
    : IRequestHandler<GetApplicationSchemesQuery, IReadOnlyList<ApplicationSchemeOptionResponse>>
{
    public async Task<IReadOnlyList<ApplicationSchemeOptionResponse>> Handle(
        GetApplicationSchemesQuery request,
        CancellationToken ct) =>
        await db.Schemes.AsNoTracking()
            .OrderBy(row => row.SortOrder)
            .Select(row => new ApplicationSchemeOptionResponse(row.Id, row.Code, row.Name))
            .ToListAsync(ct);
}

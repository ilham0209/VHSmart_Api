using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Dashboard;

// Spec 13 "Recently Added Raw Material" [CONFIRMED]: the name (the master list's Ingredient
// column), the owner company and the timestamp of the newest materials. The static card text
// ("New Material has been added") and the "View All Raw Material" link are frontend. The
// visibility is the master list's shared-visibility set (CodingRules 7.3) so the panel and
// the screen it links to show the same rows. Newest first (SysDateCreated, Id breaking ties);
// the panel size is not documented, so the caller picks it - default 10, max 50 [flagged].
public record GetRecentlyAddedRawMaterialsQuery(int Limit = 10)
    : IRequest<IReadOnlyList<RecentlyAddedRawMaterialResponse>>;

public class GetRecentlyAddedRawMaterialsValidator
    : AbstractValidator<GetRecentlyAddedRawMaterialsQuery>
{
    public GetRecentlyAddedRawMaterialsValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 50);
    }
}

public record RecentlyAddedRawMaterialResponse(
    Guid Id,
    string? Ingredient,
    string Company,
    DateTime AddedAt);

public class GetRecentlyAddedRawMaterialsHandler(VHSmartDbContext db)
    : IRequestHandler<GetRecentlyAddedRawMaterialsQuery, IReadOnlyList<RecentlyAddedRawMaterialResponse>>
{
    public async Task<IReadOnlyList<RecentlyAddedRawMaterialResponse>> Handle(
        GetRecentlyAddedRawMaterialsQuery request,
        CancellationToken ct)
    {
        return await db.RawMaterials
            .AsNoTracking()
            .OrderByDescending(row => row.SysDateCreated)
            .ThenByDescending(row => row.Id)
            .Take(request.Limit)
            .Select(row => new RecentlyAddedRawMaterialResponse(
                row.Id,
                row.Ingredient,
                db.Companies
                    .Where(company => company.Id == row.CompanyId)
                    .Select(company => company.Name)
                    .FirstOrDefault() ?? string.Empty,
                row.SysDateCreated))
            .ToListAsync(ct);
    }
}

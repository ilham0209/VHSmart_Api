using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Admin.Lookups;

// Country dropdown behind the reference lookups (Database.md 14: the full ISO 3166-1 list).
// Global read-only rows, so the handler needs no ICurrentUser - every company sees the same
// countries (R-01).
public record GetCountriesQuery : IRequest<IReadOnlyList<GetCountriesResponse>>;

public record GetCountriesResponse(Guid Id, string Name, string IsoCode);

public class GetCountriesHandler(VHSmartDbContext db)
    : IRequestHandler<GetCountriesQuery, IReadOnlyList<GetCountriesResponse>>
{
    public async Task<IReadOnlyList<GetCountriesResponse>> Handle(
        GetCountriesQuery request,
        CancellationToken ct) =>
        await db.Countries
            .AsNoTracking()
            .OrderBy(country => country.Name)
            .Select(country => new GetCountriesResponse(country.Id, country.Name, country.IsoCode))
            .ToListAsync(ct);
}

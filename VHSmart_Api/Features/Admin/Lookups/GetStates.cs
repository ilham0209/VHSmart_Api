using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Admin.Lookups;

// State dropdown behind the reference lookups. Seeded for Malaysia only: every other country
// keeps a free-text state on the owning record (spec 7.1), so asking for the states of such a
// country is not an error - it returns an empty list and the form shows a text box (R-01).
public record GetStatesQuery(Guid? CountryId) : IRequest<IReadOnlyList<GetStatesResponse>>;

public record GetStatesResponse(Guid Id, Guid CountryId, string Name);

public class GetStatesHandler(VHSmartDbContext db)
    : IRequestHandler<GetStatesQuery, IReadOnlyList<GetStatesResponse>>
{
    public async Task<IReadOnlyList<GetStatesResponse>> Handle(
        GetStatesQuery request,
        CancellationToken ct)
    {
        var states = db.States.AsNoTracking();

        if (request.CountryId is Guid countryId)
            states = states.Where(state => state.CountryId == countryId);

        return await states
            .OrderBy(state => state.Name)
            .Select(state => new GetStatesResponse(state.Id, state.CountryId, state.Name))
            .ToListAsync(ct);
    }
}

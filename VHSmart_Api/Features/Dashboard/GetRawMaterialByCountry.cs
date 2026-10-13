using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Dashboard;

// Spec 13 "Raw Material By Country" [CONFIRMED]: the side list "TOTAL RAW MATERIAL BY
// COUNTRY" behind the world map (map rendering is frontend). Countries come from the raw
// material's MANUFACTURER country (the spec says so explicitly; supplier country never
// counts). Rows whose manufacturer has no country land in the "Others" bucket - the
// screenshot shows that label (twice, we cannot reproduce duplicate buckets [flagged]).
// Visibility is the master list's shared-visibility set (CodingRules 7.3); order is count
// descending, name breaking ties (the screenshot's order, the tiebreak is ours).
public record GetRawMaterialByCountryQuery : IRequest<IReadOnlyList<RawMaterialByCountryResponse>>;

public record RawMaterialByCountryResponse(string Country, int Count);

public class GetRawMaterialByCountryHandler(VHSmartDbContext db)
    : IRequestHandler<GetRawMaterialByCountryQuery, IReadOnlyList<RawMaterialByCountryResponse>>
{
    private const string Others = "Others";

    public async Task<IReadOnlyList<RawMaterialByCountryResponse>> Handle(
        GetRawMaterialByCountryQuery request,
        CancellationToken ct)
    {
        var materials = await db.RawMaterials
            .AsNoTracking()
            .Select(row => new
            {
                CountryName = db.ManufacturerSuppliers
                    .Where(supplier => supplier.Id == row.ManufacturerSupplierId)
                    .Select(supplier => db.Countries
                        .Where(country => country.Id == supplier.ManufacturerCountryId)
                        .Select(country => country.Name)
                        .FirstOrDefault())
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return materials
            .GroupBy(row => string.IsNullOrWhiteSpace(row.CountryName) ? Others : row.CountryName)
            .Select(group => new RawMaterialByCountryResponse(group.Key, group.Count()))
            .OrderByDescending(row => row.Count)
            .ThenBy(row => row.Country)
            .ToList();
    }
}

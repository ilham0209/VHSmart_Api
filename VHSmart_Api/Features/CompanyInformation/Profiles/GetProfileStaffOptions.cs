using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.CompanyInformation.Profiles;

// Staff picker behind the two dropdowns of the edit form (spec 7.3: "Contact Person and
// Halal Executive are chosen from All Staff"). It is scoped to the profile's company, which
// the Personnel list cannot do - that one is always the caller's own company (P-01), so a
// platform admin editing another company's profile would have nothing to pick from. Gated by
// Company.Profiles because the General Data / staff CRUD endpoints belong to other screens
// (same reasoning as the CI-01 options endpoint).
public record GetProfileStaffOptionsQuery(Guid CompanyId)
    : IRequest<IReadOnlyList<ProfileStaffOptionResponse>>;

public record ProfileStaffOptionResponse(Guid Id, string Name, string Email, string? Designation);

public class GetProfileStaffOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetProfileStaffOptionsQuery, IReadOnlyList<ProfileStaffOptionResponse>>
{
    public async Task<IReadOnlyList<ProfileStaffOptionResponse>> Handle(
        GetProfileStaffOptionsQuery request,
        CancellationToken ct)
    {
        var company = await CompanyProfileLoader.LoadCompanyAsync(
            db, user, request.CompanyId, ct);

        var staff = await db.Staffs
            .AsNoTracking()
            .Where(row => row.CompanyId == company.Id)
            .OrderBy(row => row.Name)
            .Select(row => new { row.Id, row.Name, row.Email, row.DesignationId })
            .ToListAsync(ct);

        var designationIds = staff.Select(row => row.DesignationId).Distinct().ToList();
        var designations = await db.GeneralData
            .AsNoTracking()
            .Where(row => designationIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, row => row.Name, ct);

        return staff
            .Select(row => new ProfileStaffOptionResponse(
                row.Id,
                row.Name,
                row.Email,
                designations.GetValueOrDefault(row.DesignationId)))
            .ToList();
    }
}

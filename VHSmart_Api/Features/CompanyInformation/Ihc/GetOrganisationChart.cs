using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.CompanyInformation.Ihc;

// Organisation Chart (spec 7.6, CONFIRMED): the chart tree itself (Halal Chairman on top,
// executives below, then supervisors/slaughtermen) is a client-side layout - the API serves
// the table underneath it: staff flagged as IHC members WITH a role, columns #, Name, Role,
// Department, Email, Mobile No., Company Name (the Indicator dot maps role -> colour in the
// client). Roles are free General Data values (Database.md gives no list), so the default
// order is role name then staff name; with the canonical role names ("Halal Chairman",
// "Halal Executive", "Halal Supervisor", "Slaughterman") that reproduces the spec's top-down
// chart order exactly. This is not a DataGrid - the spec shows no search/paging on it - and
// it is own-company only, the same Switch Company stance CI-01/P-01 took.
public record GetOrganisationChartQuery : IRequest<IReadOnlyList<OrganisationChartResponse>>;

public record OrganisationChartResponse(
    Guid StaffId,
    int No,
    string Name,
    string Role,
    string Department,
    string Email,
    string MobileNo,
    string Company);

public class GetOrganisationChartHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetOrganisationChartQuery, IReadOnlyList<OrganisationChartResponse>>
{
    public async Task<IReadOnlyList<OrganisationChartResponse>> Handle(
        GetOrganisationChartQuery request,
        CancellationToken ct)
    {
        var companyId = user.CompanyId;
        var companyName = await db.Companies
            .AsNoTracking()
            .Where(row => row.Id == companyId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        // The tenant filter hides soft-deleted staff; IhcRoleId != null is the "with a role"
        // half of the spec sentence (7.6).
        var rows = await (
                from staff in db.Staffs.AsNoTracking()
                    .Where(row => row.CompanyId == companyId
                        && row.IsIhcMember
                        && row.IhcRoleId != null)
                orderby staff.IhcRole!.Name, staff.Name
                select new
                {
                    staff.Id,
                    staff.Name,
                    Role = staff.IhcRole == null ? string.Empty : staff.IhcRole.Name,
                    Department = staff.Department == null ? string.Empty : staff.Department.Name,
                    staff.Email,
                    staff.MobileNumber
                })
            .ToListAsync(ct);

        return [.. rows.Select((row, index) => new OrganisationChartResponse(
            row.Id,
            index + 1,
            row.Name,
            row.Role,
            row.Department,
            row.Email,
            row.MobileNumber ?? string.Empty,
            companyName))];
    }
}

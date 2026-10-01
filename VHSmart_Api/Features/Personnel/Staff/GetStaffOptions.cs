using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Personnel.Staff;

// The dropdown lists of the Manage Staff modal (spec 7.4: Title*, Department*, Designation*,
// IHC Role when "Are you IHC member?" = Yes). The values are per-company AdmGeneralData rows
// (spec 3.3, Database.md 3 - General Data CRUD is Admin-gated), so this query is gated by
// Personnel.AllStaff: a company user fills the staff form without the Admin.GeneralData
// permission (same reasoning as GetCompanyGeneralOptions in CI-01). Rows are filtered to the
// caller's company explicitly - a ViewAll token must not read another tenant's lists.
public record GetStaffOptionsQuery : IRequest<GetStaffOptionsResponse>;

public record GeneralDataOptionResponse(Guid Id, string Name);

public record GetStaffOptionsResponse(
    IReadOnlyList<GeneralDataOptionResponse> Titles,
    IReadOnlyList<GeneralDataOptionResponse> Designations,
    IReadOnlyList<GeneralDataOptionResponse> Departments,
    IReadOnlyList<GeneralDataOptionResponse> IhcRoles);

public class GetStaffOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetStaffOptionsQuery, GetStaffOptionsResponse>
{
    public async Task<GetStaffOptionsResponse> Handle(
        GetStaffOptionsQuery request,
        CancellationToken ct)
    {
        // One small per-company lookup; the four lists are cut from it in memory - the PEOPLE
        // group only ever holds the four categories of this form.
        var rows = await db.GeneralData
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId && row.Group == GeneralDataGroup.PEOPLE)
            .ToListAsync(ct);

        return new GetStaffOptionsResponse(
            List(rows, "Title of Honour"),
            List(rows, "Designation"),
            List(rows, "Department"),
            List(rows, "Internal Halal Committee Role"));
    }

    private static IReadOnlyList<GeneralDataOptionResponse> List(
        IEnumerable<GeneralDataEntity> rows,
        string category) =>
        [.. rows
            .Where(row => row.Category == category)
            .OrderBy(row => row.Name)
            .Select(row => new GeneralDataOptionResponse(row.Id, row.Name))];
}

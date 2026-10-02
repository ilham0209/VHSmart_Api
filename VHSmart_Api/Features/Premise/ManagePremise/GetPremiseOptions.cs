using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Premise.ManagePremise;

// The dropdown sources of the "Premise Information" and "Facility Information" tabs and of
// the list's "Premise Type" filter (spec 7.7): the four premise types (code-backed enum,
// served so the client labels match the stored values), Brand, Prayer Room Availability and
// Premise Tag (Tag column / Update Premise Tag) from the caller's own COMPANY General Data
// (spec 7.5 / D-13: these live per company and are only editable through the Admin General
// Data screen, which a company user cannot reach - same reasoning as the
// CI-01/P-01/CI-03 options endpoints), plus the staff pick
// list for Premise Manager / Area Manager / Operation Manager / Contact Person (the spec
// sources those from All Staff). One endpoint behind the Premise.ManagePremise View action
// so the screen works without the Admin.GeneralData or Personnel.AllStaff keys. Countries
// come from the existing GET api/admin/countries lookup (R-01). Rows are explicitly scoped
// to the caller's company - a ViewAll token must not read another tenant's data.
public record GetPremiseOptionsQuery : IRequest<PremiseOptionsResponse>;

public record PremiseOptionsResponse(
    IReadOnlyList<string> PremiseTypes,
    IReadOnlyList<PremiseGeneralDataOption> Brands,
    IReadOnlyList<PremiseGeneralDataOption> PrayerRoomAvailabilities,
    IReadOnlyList<PremiseGeneralDataOption> PremiseTags,
    IReadOnlyList<PremiseStaffOptionResponse> Staff);

public record PremiseGeneralDataOption(Guid Id, string Name);

public record PremiseStaffOptionResponse(
    Guid Id,
    string Name,
    string Designation,
    string Email,
    string? MobileNumber);

public class GetPremiseOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetPremiseOptionsQuery, PremiseOptionsResponse>
{
    public async Task<PremiseOptionsResponse> Handle(
        GetPremiseOptionsQuery request,
        CancellationToken ct)
    {
        var brands = await db.GeneralData
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.Group == GeneralDataGroup.COMPANY
                && row.Category == "Brand")
            .OrderBy(row => row.Name)
            .Select(row => new PremiseGeneralDataOption(row.Id, row.Name))
            .ToListAsync(ct);

        var prayerRoomAvailabilities = await db.GeneralData
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.Group == GeneralDataGroup.COMPANY
                && row.Category == "Prayer Room Availability")
            .OrderBy(row => row.Name)
            .Select(row => new PremiseGeneralDataOption(row.Id, row.Name))
            .ToListAsync(ct);

        // Premise Tag values for the Tag column / "Update Premise Tag" (spec 7.7): the same
        // COMPANY General Data pattern as Brand.
        var premiseTags = await db.GeneralData
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.Group == GeneralDataGroup.COMPANY
                && row.Category == "Premise Tag")
            .OrderBy(row => row.Name)
            .Select(row => new PremiseGeneralDataOption(row.Id, row.Name))
            .ToListAsync(ct);

        var staff = await db.Staffs
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .OrderBy(row => row.Name)
            .Select(row => new PremiseStaffOptionResponse(
                row.Id,
                row.Name,
                row.Designation == null ? string.Empty : row.Designation.Name,
                row.Email,
                row.MobileNumber))
            .ToListAsync(ct);

        return new PremiseOptionsResponse(
            [.. Enum.GetNames<PremiseType>()],
            brands,
            prayerRoomAvailabilities,
            premiseTags,
            staff);
    }
}

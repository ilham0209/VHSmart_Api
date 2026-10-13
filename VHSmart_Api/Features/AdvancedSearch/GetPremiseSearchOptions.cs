using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.AdvancedSearch;

// The filter dropdown sources of Advanced Search > Premise (spec 8): status lists are the
// screen-local values the filters actually match - Premise Status and Halal Application
// Status as the distinct observed values (free-text stored values, e.g. "ACTIVE" - ours,
// flagged), Premise Document Status as the canonical D-15 texts the calculator can answer,
// Halal Certificate Status as the two D-04 values - plus Brand, Premise Tag (the same COMPANY
// General Data pattern as the Premise options endpoint), Menu Concept and the staff pick
// list, all explicitly scoped to the caller's company. Countries and States reuse the
// existing GET api/admin/countries + states lookups (spec 8: "Country/State"), so they are
// not duplicated here.
public record GetPremiseSearchOptionsQuery : IRequest<PremiseSearchOptionsResponse>;

public record PremiseSearchOptionsResponse(
    IReadOnlyList<string> PremiseStatuses,
    IReadOnlyList<string> PremiseDocumentStatuses,
    IReadOnlyList<string> HalalApplicationStatuses,
    IReadOnlyList<string> HalalCertificateStatuses,
    IReadOnlyList<PremiseSearchOption> Brands,
    IReadOnlyList<PremiseSearchOption> PremiseTags,
    IReadOnlyList<PremiseSearchOption> MenuConcepts,
    IReadOnlyList<PremiseSearchOption> Staff);

public record PremiseSearchOption(Guid Id, string Name);

public class GetPremiseSearchOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetPremiseSearchOptionsQuery, PremiseSearchOptionsResponse>
{
    public async Task<PremiseSearchOptionsResponse> Handle(
        GetPremiseSearchOptionsQuery request,
        CancellationToken ct)
    {
        var premiseStatuses = await db.Premises
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .Select(row => row.Status)
            .Distinct()
            .OrderBy(status => status)
            .ToListAsync(ct);

        var documentStatuses = new List<string>
        {
            PremiseDocumentStatusCalculator.CompleteText,
            PremiseDocumentStatusCalculator.NotCompleteText
        };
        for (var expiredCount = 1;
             expiredCount <= PremiseDocumentStatusCalculator.RequiredDocumentTypes.Count;
             expiredCount++)
        {
            documentStatuses.Add(PremiseDocumentStatusCalculator.ExpiredText(expiredCount));
        }

        var applicationStatuses = await db.Applications
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .Select(row => row.Status)
            .Distinct()
            .OrderBy(status => status)
            .ToListAsync(ct);

        var brands = await db.GeneralData
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.Group == GeneralDataGroup.COMPANY
                && row.Category == "Brand")
            .OrderBy(row => row.Name)
            .Select(row => new PremiseSearchOption(row.Id, row.Name))
            .ToListAsync(ct);

        var premiseTags = await db.GeneralData
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.Group == GeneralDataGroup.COMPANY
                && row.Category == "Premise Tag")
            .OrderBy(row => row.Name)
            .Select(row => new PremiseSearchOption(row.Id, row.Name))
            .ToListAsync(ct);

        var menuConcepts = await db.MenuConcepts
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .OrderBy(row => row.Name)
            .Select(row => new PremiseSearchOption(row.Id, row.Name ?? string.Empty))
            .ToListAsync(ct);

        var staff = await db.Staffs
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .OrderBy(row => row.Name)
            .Select(row => new PremiseSearchOption(row.Id, row.Name ?? string.Empty))
            .ToListAsync(ct);

        return new PremiseSearchOptionsResponse(
            premiseStatuses,
            documentStatuses,
            applicationStatuses,
            [nameof(HalalStatus.Valid), nameof(HalalStatus.Expired)],
            brands,
            premiseTags,
            menuConcepts,
            staff);
    }
}

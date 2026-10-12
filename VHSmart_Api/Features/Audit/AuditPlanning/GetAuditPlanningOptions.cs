using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditPlanning;

// The pickers of the Audit Planning create modal (spec 14.8 [CONFIRMED / MANUAL]):
// Audit Purpose*, Audit Type*, Category* - the General Data pairs of the spec 14.1
// probable mapping (flagged, same as the validator) - Details* (the caller's own premises:
// the manual example shows Category = PREMISE and Details = the premise name) and Assign
// by Group* (own-company groups with status Active ONLY - [CONFIRMED]). Each group option
// carries its single checklist (AU-06 radio) plus the display label the manual shows
// ("Technical Category - checklist tech"), so the disabled Template Selection can render
// the moment a group is chosen. Explicitly company scoped so a ViewAll token must not read
// another tenant's rows; behind Audit.AuditPlanning View so the screen needs no other
// menu's permission.
public record GetAuditPlanningOptionsQuery : IRequest<AuditPlanningOptionsResponse>;

public record AuditPlanningOptionsResponse(
    IReadOnlyList<AuditPlanningOption> Purposes,
    IReadOnlyList<AuditPlanningOption> Types,
    IReadOnlyList<AuditPlanningOption> Categories,
    IReadOnlyList<AuditPlanningPremiseOption> Premises,
    IReadOnlyList<AuditPlanningGroupOption> Groups);

public record AuditPlanningOption(Guid Id, string Name);

public record AuditPlanningPremiseOption(Guid Id, string Name, string? StoreCode);

public record AuditPlanningGroupOption(
    Guid Id,
    string Name,
    Guid ChecklistId,
    string ChecklistLabel);

public class GetAuditPlanningOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAuditPlanningOptionsQuery, AuditPlanningOptionsResponse>
{
    public async Task<AuditPlanningOptionsResponse> Handle(
        GetAuditPlanningOptionsQuery request,
        CancellationToken ct)
    {
        var purposes = await GeneralDataOptionsAsync(
            db, user.CompanyId, CreateAuditPlanValidator.AuditPurposeCategory, ct);
        var types = await GeneralDataOptionsAsync(
            db, user.CompanyId, CreateAuditPlanValidator.AuditTypeCategory, ct);
        var categories = await GeneralDataOptionsAsync(
            db, user.CompanyId, CreateAuditPlanValidator.AuditCategory, ct);

        var premises = await db.Premises
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .Select(row => new AuditPlanningPremiseOption(row.Id, row.Name, row.StoreCode))
            .OrderBy(row => row.Name)
            .ToListAsync(ct);

        var groups = await db.GroupAuditors
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.Status == GroupAuditorStatus.Active)
            .Select(row => new AuditPlanningGroupOption(
                row.Id,
                row.Name,
                row.ChecklistId,
                // The manual's item text: "{category} - {checklist name}".
                ((from category in db.GeneralData
                  where category.Id == (from checklist in db.AuditChecklists
                                        where checklist.Id == row.ChecklistId
                                        select checklist.ChecklistCategoryId).FirstOrDefault()
                  select category.Name).FirstOrDefault() ?? string.Empty)
                + " - "
                + (from checklist in db.AuditChecklists
                   where checklist.Id == row.ChecklistId
                   select checklist.Name).FirstOrDefault()))
            .OrderBy(row => row.Name)
            .ToListAsync(ct);

        return new AuditPlanningOptionsResponse(purposes, types, categories, premises, groups);
    }

    internal static async Task<IReadOnlyList<AuditPlanningOption>> GeneralDataOptionsAsync(
        VHSmartDbContext db,
        Guid companyId,
        string category,
        CancellationToken ct) =>
        await db.GeneralData
            .AsNoTracking()
            .Where(row => row.CompanyId == companyId
                && row.Group == GeneralDataGroup.AUDIT
                && row.Category == category)
            .Select(row => new AuditPlanningOption(row.Id, row.Name))
            .OrderBy(row => row.Name)
            .ToListAsync(ct);
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.GroupAuditor;

// The two pickers of the add/edit modal (spec 14.7 [CONFIRMED]): the Template Selection
// radio table (columns Template Category, Template Name) - the caller's own live Audit
// Checklists with their joined General Data category name - and the auditor table
// (Select, Group Leader, Name, Contact No, Email - Group Leader is a client-side tick
// state, not a column of this list): the D-10 auditors - AdmUsers rows whose RoleId is
// the seeded "Auditor / Chief Auditor" role (RoleSeedData.AuditorRoleId) that belong to
// the caller's company via AdmUserCompanies. The spec 14.7 [VERIFY] "source list not
// confirmed" is resolved by D-10; the spec confirms nothing about IsActive, so
// deactivated auditors remain listed (flagged). Templates default-sort by category then
// name (the visible columns' order); auditors sort by name. Explicitly company scoped so
// a ViewAll token must not read another tenant's rows; behind Audit.AuditGroupAuditor
// View so the screen needs no other menu's permission.
public record GetGroupAuditorOptionsQuery : IRequest<GroupAuditorOptionsResponse>;

public record GroupAuditorOptionsResponse(
    IReadOnlyList<GroupAuditorTemplateOption> Templates,
    IReadOnlyList<GroupAuditorAuditorOption> Auditors);

public record GroupAuditorTemplateOption(Guid Id, string Category, string Name);

public record GroupAuditorAuditorOption(
    Guid Id,
    string Name,
    string? ContactNo,
    string? Email);

public class GetGroupAuditorOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetGroupAuditorOptionsQuery, GroupAuditorOptionsResponse>
{
    public async Task<GroupAuditorOptionsResponse> Handle(
        GetGroupAuditorOptionsQuery request,
        CancellationToken ct)
    {
        var templates = await db.AuditChecklists
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .Select(row => new GroupAuditorTemplateOption(
                row.Id,
                (from category in db.GeneralData
                 where category.Id == row.ChecklistCategoryId
                 select category.Name).FirstOrDefault() ?? string.Empty,
                row.Name))
            .OrderBy(row => row.Category)
            .ThenBy(row => row.Name)
            .ToListAsync(ct);

        var auditors = await db.Users
            .AsNoTracking()
            .Where(row => row.RoleId == RoleSeedData.AuditorRoleId
                && db.UserCompanies.Any(membership =>
                    membership.UserId == row.Id
                    && membership.CompanyId == user.CompanyId))
            .Select(row => new GroupAuditorAuditorOption(
                row.Id, row.Name, row.ContactNo, row.Email))
            .OrderBy(row => row.Name)
            .ToListAsync(ct);

        return new GroupAuditorOptionsResponse(templates, auditors);
    }
}

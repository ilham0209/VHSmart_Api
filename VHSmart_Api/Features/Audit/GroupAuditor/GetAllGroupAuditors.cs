using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Audit.GroupAuditor;

// Group list (spec 14.7 "List Group Auditor"): Action is client side (only the Id is
// needed), Group Name is the own column, Group Leader Name is the leader's joined AdmUsers
// name (correlated subquery; searchable via the AU-04 response-prop stance), Auditor Name
// is the comma-joined names of the members filled for THIS page after paging (the AU-03
// RM-02 stance - a collection never decides order, so it is never searched; flagged), and
// Group Status is the enum (not searched - the reflection search is string-props only;
// flagged). The spec states no default order, so Group Name ascending is used (flagged).
// The explicit CompanyId match keeps a Switch Company = ALL caller on their own groups.
public record GetAllGroupAuditorsQuery : IRequest<DataGridResponse<GetAllGroupAuditorsResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllGroupAuditorsResponse(
    Guid Id,
    string GroupName,
    string GroupLeaderName,
    string AuditorName,
    GroupAuditorStatus Status);

public class GetAllGroupAuditorsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllGroupAuditorsQuery, DataGridResponse<GetAllGroupAuditorsResponse>>
{
    public async Task<DataGridResponse<GetAllGroupAuditorsResponse>> Handle(
        GetAllGroupAuditorsQuery request,
        CancellationToken ct)
    {
        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(GetAllGroupAuditorsResponse.GroupName)
            : request.Request.SortBy;

        var grid = await db.GroupAuditors
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .Select(row => new GetAllGroupAuditorsResponse(
                row.Id,
                row.Name,
                (from member in db.Users
                 where member.Id == row.LeaderUserId
                 select member.Name).FirstOrDefault() ?? string.Empty,
                // Placeholder; the real cell is filled for THIS page after paging.
                string.Empty,
                row.Status))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(GetAllGroupAuditorsResponse.GroupName),
                nameof(GetAllGroupAuditorsResponse.GroupLeaderName))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);

        // The Auditor Name cell is the comma-joined member names, filled AFTER paging
        // (RM-02 stance; the GetAllFindings precedent).
        List<Guid> pageIds = [.. grid.Data.Select(row => row.Id)];
        if (pageIds.Count == 0)
            return grid;

        var memberNames = await (
            from link in db.GroupAuditorMembers.AsNoTracking()
            where pageIds.Contains(link.GroupAuditorId)
            select new
            {
                link.GroupAuditorId,
                Name = (from member in db.Users
                        where member.Id == link.UserId
                        select member.Name).FirstOrDefault() ?? string.Empty
            }).ToListAsync(ct);

        var namesByGroup = memberNames
            .GroupBy(row => row.GroupAuditorId)
            .ToDictionary(
                group => group.Key,
                group => string.Join(", ",
                    group
                        .Select(row => row.Name)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)));

        grid.Data = [.. grid.Data.Select(row => row with
        {
            AuditorName = namesByGroup.GetValueOrDefault(row.Id, string.Empty)
        })];

        return grid;
    }
}

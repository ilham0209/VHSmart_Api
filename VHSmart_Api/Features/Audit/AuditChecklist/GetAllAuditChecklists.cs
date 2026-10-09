using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Audit.AuditChecklist;

// Checklist list (spec 14.6 "List Audit Checklist" [CONFIRMED]): Action is client side
// (only the Id is needed); Checklist Category is the joined General Data name, Name and
// Description the row's own columns. The spec states no default order, so Checklist
// Category ascending is used (the alphabetical-first-visible-column stance of AU-02/AU-03/
// AU-04; flagged). Search covers the visible text columns AFTER the projection on the
// response properties, which is what makes the joined Category searchable at all. The
// explicit CompanyId match keeps a Switch Company = ALL caller on their own checklists
// (spec 14.0).
public record GetAllAuditChecklistsQuery : IRequest<DataGridResponse<GetAllAuditChecklistsResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllAuditChecklistsResponse(
    Guid Id,
    string ChecklistCategory,
    string Name,
    string? Description);

public class GetAllAuditChecklistsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllAuditChecklistsQuery, DataGridResponse<GetAllAuditChecklistsResponse>>
{
    public async Task<DataGridResponse<GetAllAuditChecklistsResponse>> Handle(
        GetAllAuditChecklistsQuery request,
        CancellationToken ct)
    {
        var defaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = defaultSort
            ? nameof(GetAllAuditChecklistsResponse.ChecklistCategory)
            : request.Request.SortBy;
        var descending = !defaultSort && request.Request.SortDescending;

        return await (
                from checklist in db.AuditChecklists.AsNoTracking()
                    .Where(row => row.CompanyId == user.CompanyId)
                select new GetAllAuditChecklistsResponse(
                    checklist.Id,
                    (from generalData in db.GeneralData
                     where generalData.Id == checklist.ChecklistCategoryId
                     select generalData.Name).FirstOrDefault() ?? string.Empty,
                    checklist.Name,
                    checklist.Description))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(GetAllAuditChecklistsResponse.ChecklistCategory),
                nameof(GetAllAuditChecklistsResponse.Name),
                nameof(GetAllAuditChecklistsResponse.Description))
            .ApplySort(sortBy, descending)
            .ToDataGridResponseAsync(request.Request, ct);
    }
}

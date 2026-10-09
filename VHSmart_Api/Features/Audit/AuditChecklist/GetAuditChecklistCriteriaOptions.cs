using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Audit.AuditChecklist;

// The Criteria Selection table of the add/edit modal (spec 14.6 [CONFIRMED]: columns
// Select, Category, Criteria, Sub Criteria, Reference, Potential Point, Description with
// search and paging; "No data available" until criteria exist): every live audit criteria
// row of the caller's company, paged and searchable - Select is the checkbox over the row
// Id. The [VERIFY] of spec 14.6 (is the list filtered by Checklist Category?) has no
// default in Decisions.md; it changes no data model, so it proceeds with the flagged
// assumption NOT to filter: the modal shows the Category column per row, and spec 14.9's
// task checklist groups rows under several numbered category headings (1 PEST CONTROL,
// 2 STORAGE ...), which only works if one checklist spans categories. Mirrors the
// AU-04 list shape (the house keeps a separate picker per feature - AU-03
// precedent); behind the Audit.AuditChecklist View action so the checklist screen needs
// no Audit.AuditCriteria key.
public record GetAuditChecklistCriteriaOptionsQuery
    : IRequest<DataGridResponse<AuditChecklistCriteriaOptionResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record AuditChecklistCriteriaOptionResponse(
    Guid Id,
    string Category,
    string Criteria,
    string SubCriteria,
    string? Reference,
    decimal PotentialPoint,
    string? Description);

public class GetAuditChecklistCriteriaOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<
        GetAuditChecklistCriteriaOptionsQuery,
        DataGridResponse<AuditChecklistCriteriaOptionResponse>>
{
    public async Task<DataGridResponse<AuditChecklistCriteriaOptionResponse>> Handle(
        GetAuditChecklistCriteriaOptionsQuery request,
        CancellationToken ct)
    {
        var defaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = defaultSort
            ? nameof(AuditChecklistCriteriaOptionResponse.Category)
            : request.Request.SortBy;
        var descending = !defaultSort && request.Request.SortDescending;

        return await (
                from criteria in db.AuditCriteria.AsNoTracking()
                    .Where(row => row.CompanyId == user.CompanyId)
                select new AuditChecklistCriteriaOptionResponse(
                    criteria.Id,
                    (from generalData in db.GeneralData
                     where generalData.Id == criteria.CategoryId
                     select generalData.Name).FirstOrDefault() ?? string.Empty,
                    (from master in db.AuditCriteriaMasters
                     where master.Id == criteria.CriteriaId
                     select master.Text).FirstOrDefault() ?? string.Empty,
                    criteria.SubCriteriaId == null
                        ? string.Empty
                        : (from master in db.AuditCriteriaMasters
                           where master.Id == criteria.SubCriteriaId
                           select master.Text).FirstOrDefault() ?? string.Empty,
                    criteria.Reference,
                    criteria.PotentialPoint,
                    criteria.Description))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(AuditChecklistCriteriaOptionResponse.Category),
                nameof(AuditChecklistCriteriaOptionResponse.Criteria),
                nameof(AuditChecklistCriteriaOptionResponse.SubCriteria),
                nameof(AuditChecklistCriteriaOptionResponse.Reference),
                nameof(AuditChecklistCriteriaOptionResponse.Description))
            .ApplySort(sortBy, descending)
            .ToDataGridResponseAsync(request.Request, ct);
    }
}

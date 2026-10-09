using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Audit.AuditCriteria;

// Audit Criteria list (spec 14.5 "List Audit Criteria" [CONFIRMED]): Action is client side
// (only the Id is needed); Category, Criteria and Sub Criteria are the joined General Data
// name / master texts (the checklist section heading and the reusable values); Reference,
// Potential Point and Description are the row's own columns. The spec states no default
// order, so Category ascending is used (the alphabetical-first-visible-column stance of
// AU-02/AU-03; flagged). Search covers the visible text columns - they are searched AFTER
// the projection on the response properties, the GetAllAuditPrefixes shape, which is what
// makes the joined Category / Criteria / Sub Criteria searchable at all. The explicit
// CompanyId match keeps a Switch Company = ALL caller on their own audit setup rows
// (spec 14.0).
public record GetAllAuditCriteriaQuery : IRequest<DataGridResponse<GetAllAuditCriteriaResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllAuditCriteriaResponse(
    Guid Id,
    string Category,
    string Criteria,
    string SubCriteria,
    string? Reference,
    decimal PotentialPoint,
    string? Description);

public class GetAllAuditCriteriaHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllAuditCriteriaQuery, DataGridResponse<GetAllAuditCriteriaResponse>>
{
    public async Task<DataGridResponse<GetAllAuditCriteriaResponse>> Handle(
        GetAllAuditCriteriaQuery request,
        CancellationToken ct)
    {
        var defaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = defaultSort
            ? nameof(GetAllAuditCriteriaResponse.Category)
            : request.Request.SortBy;
        var descending = !defaultSort && request.Request.SortDescending;

        return await (
                from criteria in db.AuditCriteria.AsNoTracking()
                    .Where(row => row.CompanyId == user.CompanyId)
                select new GetAllAuditCriteriaResponse(
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
                nameof(GetAllAuditCriteriaResponse.Category),
                nameof(GetAllAuditCriteriaResponse.Criteria),
                nameof(GetAllAuditCriteriaResponse.SubCriteria),
                nameof(GetAllAuditCriteriaResponse.Reference),
                nameof(GetAllAuditCriteriaResponse.Description))
            .ApplySort(sortBy, descending)
            .ToDataGridResponseAsync(request.Request, ct);
    }
}

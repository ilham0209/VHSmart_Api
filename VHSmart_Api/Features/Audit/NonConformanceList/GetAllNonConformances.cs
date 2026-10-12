using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Audit.NonConformanceList;

// "List Generated Non Conformance" (spec 14.11 [CONFIRMED]): Action is client side; the
// columns are the NC's own values plus the caller's company name (rows are tenant-scoped,
// so one value per page - fetched once, the AU-09 stance). The spec states no default
// order, so Source of NCR ascending is used (first visible data column stance, flagged).
// Status is a D-05 text constant and therefore searchable (unlike AU-08's enum). The two
// optional filters serve the Audit Task page tabs (14.9): AuditTaskId narrows to one
// task's internal NCs (the "Non Conformance List" tab), FinalOnly to the finalized set
// (the "Final Non Conformance List" tab, IsFinal per Database.md 13).
public record GetAllNonConformancesQuery : IRequest<DataGridResponse<GetAllNonConformancesResponse>>
{
    public DataGridRequest Request { get; set; } = new();

    public Guid? AuditTaskId { get; set; }

    public bool FinalOnly { get; set; }
}

public record GetAllNonConformancesResponse(
    Guid Id,
    string Source,
    string? AuditReferenceNo,
    string CompanyName,
    string? Findings,
    string? Remarks,
    string? Tags,
    string? Status);

public class GetAllNonConformancesHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllNonConformancesQuery, DataGridResponse<GetAllNonConformancesResponse>>
{
    public async Task<DataGridResponse<GetAllNonConformancesResponse>> Handle(
        GetAllNonConformancesQuery request,
        CancellationToken ct)
    {
        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(GetAllNonConformancesResponse.Source)
            : request.Request.SortBy;

        var companyName = await db.Companies.AsNoTracking()
            .Where(row => row.Id == user.CompanyId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var taskItemIdsQuery = db.AuditTaskItems.AsNoTracking()
            .Where(item => item.CompanyId == user.CompanyId
                && request.AuditTaskId != null
                && item.AuditTaskId == request.AuditTaskId.Value)
            .Select(item => item.Id);

        return await db.NonConformances
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && (!request.FinalOnly || row.IsFinal)
                && (request.AuditTaskId == null
                    || (row.AuditTaskItemId != null
                        && taskItemIdsQuery.Contains(row.AuditTaskItemId.Value))))
            .Select(row => new GetAllNonConformancesResponse(
                row.Id,
                row.Source.ToString(),
                row.AuditReferenceNo,
                companyName,
                row.Findings,
                row.Remarks,
                row.Tags,
                row.Status))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(GetAllNonConformancesResponse.Source),
                nameof(GetAllNonConformancesResponse.AuditReferenceNo),
                nameof(GetAllNonConformancesResponse.CompanyName),
                nameof(GetAllNonConformancesResponse.Findings),
                nameof(GetAllNonConformancesResponse.Remarks),
                nameof(GetAllNonConformancesResponse.Tags),
                nameof(GetAllNonConformancesResponse.Status))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);
    }
}

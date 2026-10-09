using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.HalalApplication.ManageBatch;

// Manage Batch list (spec 12.2 "List of Batches", [CONFIRMED] columns): Action is client
// side (only the Id is needed), Scheme and Brand Owner are joined display names, Premises is
// the comma-joined premise names for a Food Premise batch (empty for a product batch), Brand
// is the batch's BrandId name (the same value as Brand Owner - the schema has one BrandId;
// the frontend may render one or both), CB Application No. is always null for now (it comes
// from AppHalalApplications which HA-02+ builds), and Created Date is SysDateCreated. Search
// covers Batch Name and CB Reference No.; default order is Name ascending (the spec states
// no order). The explicit CompanyId match keeps a Switch Company = ALL caller on their own
// rows (same guard as every other list).
public record GetAllBatchesQuery : IRequest<DataGridResponse<GetAllBatchesResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllBatchesResponse(
    Guid Id,
    string Scheme,
    string BrandOwner,
    string BatchName,
    string Premises,
    string Brand,
    string? CbApplicationNo,
    DateTime? SubmissionPlannedDate,
    DateTime CreatedDate);

public class GetAllBatchesHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllBatchesQuery, DataGridResponse<GetAllBatchesResponse>>
{
    public async Task<DataGridResponse<GetAllBatchesResponse>> Handle(
        GetAllBatchesQuery request,
        CancellationToken ct)
    {
        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(GetAllBatchesResponse.BatchName)
            : request.Request.SortBy;

        var grid = await db.Batches
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(BatchEntity.Name),
                nameof(BatchEntity.CbReferenceNo))
            .Select(row => new GetAllBatchesResponse(
                row.Id,
                db.Schemes
                    .Where(scheme => scheme.Id == row.SchemeId)
                    .Select(scheme => scheme.Name)
                    .FirstOrDefault() ?? string.Empty,
                db.GeneralData
                    .Where(data => data.Id == row.BrandId)
                    .Select(data => data.Name)
                    .FirstOrDefault() ?? string.Empty,
                row.Name,
                // Placeholder; the real premise names are filled for THIS page after paging.
                string.Empty,
                db.GeneralData
                    .Where(data => data.Id == row.BrandId)
                    .Select(data => data.Name)
                    .FirstOrDefault() ?? string.Empty,
                null,
                row.SubmissionPlannedDate,
                row.SysDateCreated))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);

        // The Premises column is the comma-joined names of the batch's linked premises (only
        // Food Premise batches have any), filled AFTER paging - a collection never decides
        // the row order (RM-02 stance).
        List<Guid> pageIds = [.. grid.Data.Select(row => row.Id)];
        if (pageIds.Count == 0)
            return grid;

        var premises = await db.BatchPremises.AsNoTracking()
            .Where(row => pageIds.Contains(row.BatchId))
            .Select(row => new { row.BatchId, row.PremiseId })
            .ToListAsync(ct);
        var premiseNames = await db.Premises.AsNoTracking()
            .Where(row => premises.Select(p => p.PremiseId).Contains(row.Id))
            .Select(row => new { row.Id, row.Name })
            .ToDictionaryAsync(row => row.Id, row => row.Name, ct);

        var premisesByBatch = premises
            .GroupBy(row => row.BatchId)
            .ToDictionary(
                group => group.Key,
                // Names are joined alphabetically so the comma-joined cell is deterministic.
                group => string.Join(", ",
                    group
                        .Where(row => premiseNames.ContainsKey(row.PremiseId))
                        .Select(row => premiseNames[row.PremiseId])
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)));

        grid.Data = [.. grid.Data.Select(row => row with
        {
            Premises = premisesByBatch.GetValueOrDefault(row.Id, string.Empty)
        })];

        return grid;
    }
}

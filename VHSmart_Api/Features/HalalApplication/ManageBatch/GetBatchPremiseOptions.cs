using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.HalalApplication.ManageBatch;

// The source behind "Associate Premise" of the Food Premise batch edit modal (spec 12.2 flow
// 6 "tick the premises/establishments"): the caller's own premises minus the ones this batch
// already has a live row for, and - D-15 - only COMPLETE premises (the Batch > Associate
// Premise pick-list rule; the spec line that says so is [VERIFY] and D-15 is its default).
// The completeness set is computed BEFORE paging because it filters the row set (the premise
// counts are the small premise tables of spec 7.7, and PR-02 already computes the same value
// per page for the list). Name / StoreCode are stored columns, so search and sort work; the
// batch must belong to the caller's company (404 otherwise).
public record GetBatchPremiseOptionsQuery(Guid BatchId)
    : IRequest<DataGridResponse<BatchPremiseOptionResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record BatchPremiseOptionResponse(
    Guid Id,
    string Name,
    string? StoreCode);

public class GetBatchPremiseOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetBatchPremiseOptionsQuery, DataGridResponse<BatchPremiseOptionResponse>>
{
    public async Task<DataGridResponse<BatchPremiseOptionResponse>> Handle(
        GetBatchPremiseOptionsQuery request,
        CancellationToken ct)
    {
        var batchExists = await db.Batches
            .AsNoTracking()
            .AnyAsync(
                row => row.Id == request.BatchId && row.CompanyId == user.CompanyId, ct);
        if (!batchExists)
            throw new NotFoundException("Batch not found.");

        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(BatchPremiseOptionResponse.Name)
            : request.Request.SortBy;

        // D-15 gate over the caller's own premises (an explicit CompanyId match, so a Switch
        // Company = ALL caller never sees another tenant's premises in the picker).
        var ownPremiseIds = await db.Premises
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .Select(row => row.Id)
            .ToListAsync(ct);
        var completePremiseIds = await BatchData.LoadCompletePremiseIdsAsync(
            db, ownPremiseIds, ct);

        var linkedPremiseIds = db.BatchPremises
            .AsNoTracking()
            .Where(row => row.BatchId == request.BatchId)
            .Select(row => row.PremiseId);

        return await db.Premises
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && completePremiseIds.Contains(row.Id)
                && !linkedPremiseIds.Contains(row.Id))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(PremiseEntity.Name),
                nameof(PremiseEntity.StoreCode))
            .Select(row => new BatchPremiseOptionResponse(
                row.Id,
                row.Name,
                row.StoreCode))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);
    }
}

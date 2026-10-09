using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Audit.Finding;

// Finding list (spec 14.4 "List" [CONFIRMED]): Action is client side (only the Id is
// needed), Name / Finding Code are the row's own columns, Recommendation Selection is the
// comma-joined names of the row's linked Recommendations, and "Modified Date" is
// SysDateModified (null until first edit). The spec states no default order, so Name
// ascending is used (the GetAllPremises stance; flagged). Search covers the two own text
// columns only - Recommendation Selection is filled for THIS page after paging and never
// decides order (RM-02 stance), so it is not searched (flagged). The explicit CompanyId
// match keeps a Switch Company = ALL caller on their own audit setup rows (spec 14.0).
public record GetAllFindingsQuery : IRequest<DataGridResponse<GetAllFindingsResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllFindingsResponse(
    Guid Id,
    string Name,
    string FindingCode,
    string RecommendationSelection,
    DateTime? ModifiedDate);

public class GetAllFindingsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllFindingsQuery, DataGridResponse<GetAllFindingsResponse>>
{
    public async Task<DataGridResponse<GetAllFindingsResponse>> Handle(
        GetAllFindingsQuery request,
        CancellationToken ct)
    {
        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(GetAllFindingsResponse.Name)
            : request.Request.SortBy;

        var grid = await db.Findings
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(FindingEntity.Name),
                nameof(FindingEntity.FindingCode))
            .Select(row => new GetAllFindingsResponse(
                row.Id,
                row.Name,
                row.FindingCode,
                // Placeholder; the real selection is filled for THIS page after paging.
                string.Empty,
                row.SysDateModified))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);

        // The Recommendation Selection cell is the comma-joined names of the finding's
        // linked recommendations, filled AFTER paging - a collection never decides the row
        // order (RM-02 stance; the GetAllBatches Premises precedent).
        List<Guid> pageIds = [.. grid.Data.Select(row => row.Id)];
        if (pageIds.Count == 0)
            return grid;

        var links = await db.FindingRecommendations.AsNoTracking()
            .Where(row => pageIds.Contains(row.FindingId))
            .Select(row => new { row.FindingId, row.RecommendationId })
            .ToListAsync(ct);
        var recommendationNames = await db.Recommendations.AsNoTracking()
            .Where(row => links.Select(link => link.RecommendationId).Contains(row.Id))
            .Select(row => new { row.Id, row.Name })
            .ToDictionaryAsync(row => row.Id, row => row.Name, ct);

        var selectionByFinding = links
            .GroupBy(row => row.FindingId)
            .ToDictionary(
                group => group.Key,
                // Names are joined alphabetically so the comma-joined cell is deterministic.
                group => string.Join(", ",
                    group
                        .Where(row => recommendationNames.ContainsKey(row.RecommendationId))
                        .Select(row => recommendationNames[row.RecommendationId])
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)));

        grid.Data = [.. grid.Data.Select(row => row with
        {
            RecommendationSelection = selectionByFinding.GetValueOrDefault(row.Id, string.Empty)
        })];

        return grid;
    }
}

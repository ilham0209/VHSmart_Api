using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Audit.Recommendation;

// Recommendation list (spec 14.3 "List" [CONFIRMED]): Action is client side (only the Id is
// needed), Name / Recommendation Code / Description are the row's own columns and
// "Modified Date" is SysDateModified (null until first edit). The spec states no default
// order, so Name ascending is used (the GetAllPremises stance; flagged). Search covers all
// three visible text columns. The explicit CompanyId match keeps a Switch Company = ALL
// caller on their own audit setup rows (spec 14.0).
public record GetAllRecommendationsQuery : IRequest<DataGridResponse<GetAllRecommendationsResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllRecommendationsResponse(
    Guid Id,
    string Name,
    string RecommendationCode,
    string? Description,
    DateTime? ModifiedDate);

public class GetAllRecommendationsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllRecommendationsQuery, DataGridResponse<GetAllRecommendationsResponse>>
{
    public async Task<DataGridResponse<GetAllRecommendationsResponse>> Handle(
        GetAllRecommendationsQuery request,
        CancellationToken ct)
    {
        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(GetAllRecommendationsResponse.Name)
            : request.Request.SortBy;

        return await db.Recommendations
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(RecommendationEntity.Name),
                nameof(RecommendationEntity.RecommendationCode),
                nameof(RecommendationEntity.Description))
            .Select(row => new GetAllRecommendationsResponse(
                row.Id, row.Name, row.RecommendationCode, row.Description, row.SysDateModified))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);
    }
}

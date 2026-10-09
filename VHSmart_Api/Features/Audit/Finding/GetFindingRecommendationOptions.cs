using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Audit.Finding;

// The "Recommendation*" checkbox table of the add/edit modal (spec 14.4: "a selectable
// table (checkbox per row plus select-all; columns Name, Recommendation Code, Description,
// Last Modified Date; search and paging)"). A paged + searchable grid of the caller's own
// Recommendations behind the Audit.Finding View action so the screen works without the
// Audit.Recommendation key (the GetAuditPrefixOptions reasoning); rows are explicitly
// scoped to the caller's company so a ViewAll token must not read another tenant's data.
// The spec states no order, so Name ascending is used (the same stance as the lists).
public record GetFindingRecommendationOptionsQuery
    : IRequest<DataGridResponse<FindingRecommendationOption>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record FindingRecommendationOption(
    Guid Id,
    string Name,
    string RecommendationCode,
    string? Description,
    DateTime? ModifiedDate);

public class GetFindingRecommendationOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetFindingRecommendationOptionsQuery,
        DataGridResponse<FindingRecommendationOption>>
{
    public async Task<DataGridResponse<FindingRecommendationOption>> Handle(
        GetFindingRecommendationOptionsQuery request,
        CancellationToken ct)
    {
        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(FindingRecommendationOption.Name)
            : request.Request.SortBy;

        return await db.Recommendations
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(RecommendationEntity.Name),
                nameof(RecommendationEntity.RecommendationCode),
                nameof(RecommendationEntity.Description))
            .Select(row => new FindingRecommendationOption(
                row.Id, row.Name, row.RecommendationCode, row.Description, row.SysDateModified))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);
    }
}

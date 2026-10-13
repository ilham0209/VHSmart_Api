using FluentValidation;
using MediatR;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.AdvancedSearch;

// Advanced Search > Premise view (spec 8 [CONFIRMED]): all 19 filters come from the shared
// PremiseSearchFilters shape (one rule set for Search and Export), the grid is the standard
// DataGrid plumbing, and the two derived columns are filled after paging - Document Status
// via D-15, Halal Information as the full numbered list of applications (PremiseHalalInfoLoader,
// the same data the Premise modal's tab shows). The spec states no default order, so
// StoreName ascending is used (flagged, the Premise List stance). Action and the location
// link are client-side. The explicit CompanyId match keeps a foreign company out entirely.
public record SearchPremisesQuery : PremiseSearchFilters, IRequest<DataGridResponse<SearchPremisesResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record SearchPremisesResponse(
    Guid Id,
    string StoreCode,
    string StoreName,
    string Address,
    string Telephone,
    string Email,
    string? PremiseLocationUrl,
    string Brand,
    string MenuConcept,
    Guid? AreaManagerStaffId,
    string AreaManager,
    Guid? OperationManagerStaffId,
    string OperationManager,
    IReadOnlyList<PremiseHalalInfoRow>? HalalInformation,
    string PremiseDocumentStatus,
    Guid? PremiseTagId,
    string PremiseTag);

public class SearchPremisesHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<SearchPremisesQuery, DataGridResponse<SearchPremisesResponse>>
{
    public async Task<DataGridResponse<SearchPremisesResponse>> Handle(
        SearchPremisesQuery request,
        CancellationToken ct)
    {
        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(SearchPremisesResponse.StoreName)
            : request.Request.SortBy;
        var sortDescending = request.Request.SortDescending;

        var baseQuery = await PremiseSearch.BuildQueryAsync(db, user, request, ct);
        var grid = await PremiseSearch
            .Project(baseQuery, db)
            .ApplySort(sortBy, sortDescending)
            .ToDataGridResponseAsync(request.Request, ct);

        // Derived columns are computed over the page only (the calculator is pure and needs
        // the whole required-type set per premise), never stored and never sorted on - the
        // same stance as the Premise List (spec 7.7).
        List<Guid> pageIds = [.. grid.Data.Select(row => row.Id)];
        if (pageIds.Count == 0)
            return grid;

        var today = PremiseDocumentClock.Today();
        var documentStatuses = await PremiseSearch.DocumentStatusesAsync(db, pageIds, today, ct);
        var halalByPremise = await PremiseHalalInfoLoader.LoadAsync(db, user, pageIds, ct);
        grid.Data = [.. grid.Data.Select(row => row with
        {
            PremiseDocumentStatus = documentStatuses[row.Id],
            HalalInformation = halalByPremise.GetValueOrDefault(row.Id) ?? []
        })];

        return grid;
    }
}

public class SearchPremisesValidator : AbstractValidator<SearchPremisesQuery>
{
    public SearchPremisesValidator()
    {
        PremiseSearchFilterRules.Apply(this);
    }
}

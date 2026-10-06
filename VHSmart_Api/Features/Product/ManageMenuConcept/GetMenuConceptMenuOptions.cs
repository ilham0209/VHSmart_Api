using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Product.ManageMenuConcept;

// The source of the concept's "Link" button (spec 9.3): the menus the caller may link to this
// concept, MINUS the pairs the concept already has a live row for - a pair unlinked by an
// earlier Save keeps its row as INACTIVE and stays on the concept's own table with its own
// Link icon (the PD-02 rule), so it is offered again only after it is gone from there. Column
// set is the spec's "List of Menu" (Menu Name, Menu Category). Visibility is the "Accessible
// For" filter of CodingRules 7.3, so a menu shared with the caller is offered and a foreign,
// unshared one is not; the category is filled AFTER paging because it belongs to the menu's
// own company's reference data (RM-02 stance - joined data never takes part in search/sort).
public record GetMenuConceptMenuOptionsQuery(Guid MenuConceptId)
    : IRequest<DataGridResponse<MenuConceptMenuOptionResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record MenuConceptMenuOptionResponse(
    Guid Id,
    string? Name,
    string? Category,
    DateTime? ModifiedDate);

// The scalar projection the grid pages over; Category is added after paging.
internal record MenuConceptMenuOptionRow(Guid Id, string? Name, DateTime? ModifiedDate);

public class GetMenuConceptMenuOptionsHandler(VHSmartDbContext db)
    : IRequestHandler<GetMenuConceptMenuOptionsQuery, DataGridResponse<MenuConceptMenuOptionResponse>>
{
    public async Task<DataGridResponse<MenuConceptMenuOptionResponse>> Handle(
        GetMenuConceptMenuOptionsQuery request,
        CancellationToken ct)
    {
        // The tenant filter makes an unknown, foreign concept answer 404 before anything is
        // offered for it.
        var conceptExists = await db.MenuConcepts
            .AsNoTracking()
            .AnyAsync(row => row.Id == request.MenuConceptId, ct);
        if (!conceptExists)
            throw new NotFoundException("Menu concept not found.");

        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(MenuConceptMenuOptionResponse.Name)
            : request.Request.SortBy;

        var linkedMenuIds = db.MenuConceptMenus
            .AsNoTracking()
            .Where(row => row.MenuConceptId == request.MenuConceptId)
            .Select(row => row.MenuId);

        var grid = await db.Menus
            .AsNoTracking()
            .Where(row => !linkedMenuIds.Contains(row.Id))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(MenuEntity.Name),
                nameof(MenuEntity.Description))
            .Select(row => new MenuConceptMenuOptionRow(
                row.Id,
                row.Name,
                row.SysDateModified))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);

        var menuInfo = await MenuConceptData.LoadMenuInfoAsync(
            db, grid.Data.Select(row => row.Id).ToList(), ct);

        return new DataGridResponse<MenuConceptMenuOptionResponse>
        {
            Data = [.. grid.Data.Select(row => new MenuConceptMenuOptionResponse(
                row.Id,
                row.Name,
                menuInfo.GetValueOrDefault(row.Id)?.Category,
                row.ModifiedDate))],
            TotalRecords = grid.TotalRecords,
            TotalPages = grid.TotalPages,
            CurrentPage = grid.CurrentPage,
            PageSize = grid.PageSize,
            HasNextPage = grid.HasNextPage,
            HasPreviousPage = grid.HasPreviousPage
        };
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Product.ManageMenu;

// Manage Menu list (spec 9.2 "List of Menu", [CONFIRMED] columns): Action is client side, Menu
// and Description are the form fields, Category is the AdmGeneralData name, Validity Date is
// Start Date - End Date (the client formats it, "Permanent" needs no dates), and List of
// Ingredients / List of Company are the two child lists. Status answers the stored string
// (ACTIVE from creation). Modified Date is the standard audit column this codebase adds to
// every list. Visibility is the special filter of CodingRules 7.3 (owner OR listed in
// "List of Company" OR Switch Company = ALL), configured once on the entity - this handler
// just queries the set.
// The three joined columns are filled AFTER paging: the search and the sort touch only scalar
// columns (RM-02 stance), a collection can never decide the row order, and the child rows of a
// shared menu belong to another company's tenant scope, so they are read scoped to the page.
public record GetAllMenusQuery : IRequest<DataGridResponse<GetAllMenusResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllMenusResponse(
    Guid Id,
    string? Name,
    string? Description,
    string? Category,
    DateTime? StartDate,
    DateTime? EndDate,
    string Status,
    IReadOnlyList<string> Ingredients,
    IReadOnlyList<string> Companies,
    DateTime? ModifiedDate);

// The scalar projection the grid pages over. The three joined columns are NOT here: they are
// filled after paging (RM-02 stance), so a collection never takes part in the SQL, in the
// search or in the sort - and the child rows of a shared menu belong to another company's
// tenant scope, so they are read scoped to the page of ids the filter already approved.
internal record MenuListRow(
    Guid Id,
    string? Name,
    string? Description,
    DateTime? StartDate,
    DateTime? EndDate,
    string Status,
    DateTime? ModifiedDate);

public class GetAllMenusHandler(VHSmartDbContext db)
    : IRequestHandler<GetAllMenusQuery, DataGridResponse<GetAllMenusResponse>>
{
    public async Task<DataGridResponse<GetAllMenusResponse>> Handle(
        GetAllMenusQuery request,
        CancellationToken ct)
    {
        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(GetAllMenusResponse.Name)
            : request.Request.SortBy;

        var page = await db.Menus
            .AsNoTracking()
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(MenuEntity.Name),
                nameof(MenuEntity.Description))
            .Select(row => new MenuListRow(
                row.Id,
                row.Name,
                row.Description,
                row.StartDate,
                row.EndDate,
                row.Status,
                row.SysDateModified))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);

        Guid[] menuIds = [.. page.Data.Select(row => row.Id)];
        var categories = menuIds.Length == 0
            ? new Dictionary<Guid, string?>()
            : await MenuListData.LoadCategoriesAsync(db, menuIds, ct);
        var ingredients = menuIds.Length == 0
            ? new Dictionary<Guid, IReadOnlyList<string>>()
            : await MenuIngredientListLoader.LoadNamesAsync(db, menuIds, ct);
        var companies = menuIds.Length == 0
            ? new Dictionary<Guid, IReadOnlyList<string>>()
            : await MenuListData.LoadCompanyNamesAsync(db, menuIds, ct);

        return new DataGridResponse<GetAllMenusResponse>
        {
            Data = [.. page.Data.Select(row => new GetAllMenusResponse(
                row.Id,
                row.Name,
                row.Description,
                categories.GetValueOrDefault(row.Id),
                row.StartDate,
                row.EndDate,
                row.Status,
                ingredients.GetValueOrDefault(row.Id) ?? Array.Empty<string>(),
                companies.GetValueOrDefault(row.Id) ?? Array.Empty<string>(),
                row.ModifiedDate))],
            TotalRecords = page.TotalRecords,
            TotalPages = page.TotalPages,
            CurrentPage = page.CurrentPage,
            PageSize = page.PageSize,
            HasNextPage = page.HasNextPage,
            HasPreviousPage = page.HasPreviousPage
        };
    }
}

// The two joined columns of the list that are NOT ingredient rows: the menu's category (owner
// reference data) and the names in "List of Company". Both are read scoped to the page of menu
// ids the visibility filter has already approved - IgnoreQueryFilters only lifts the TENANT
// filter of the referenced row, the soft-delete flag is checked by hand, and no id reaches a
// query that a visible menu did not name.
internal static class MenuListData
{
    public static async Task<IReadOnlyDictionary<Guid, string?>> LoadCategoriesAsync(
        VHSmartDbContext db,
        IReadOnlyCollection<Guid> menuIds,
        CancellationToken ct)
    {
        var menus = await db.Menus
            .AsNoTracking()
            .Where(row => menuIds.Contains(row.Id))
            .Select(row => new { row.Id, row.CategoryId })
            .ToListAsync(ct);

        var categoryIds = menus.Select(row => row.CategoryId).Distinct().ToList();
        var categories = await db.GeneralData
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => categoryIds.Contains(row.Id) && !row.IsDeleted)
            .Select(row => new { row.Id, row.Name })
            .ToListAsync(ct);
        var nameById = categories.ToDictionary(row => row.Id, row => row.Name);

        return menus.ToDictionary(
            row => row.Id,
            row => nameById.GetValueOrDefault(row.CategoryId));
    }

    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> LoadCompanyNamesAsync(
        VHSmartDbContext db,
        IReadOnlyCollection<Guid> menuIds,
        CancellationToken ct)
    {
        var accessRows = await db.MenuAccessibleCompanies
            .AsNoTracking()
            .Where(row => menuIds.Contains(row.MenuId))
            .Select(row => new { row.MenuId, row.AccessibleCompanyId })
            .ToListAsync(ct);

        var companyIds = accessRows.Select(row => row.AccessibleCompanyId).Distinct().ToList();
        var companies = await db.Companies
            .AsNoTracking()
            .Where(row => companyIds.Contains(row.Id))
            .Select(row => new { row.Id, row.Name })
            .ToListAsync(ct);
        var nameById = companies.ToDictionary(row => row.Id, row => row.Name);

        return accessRows
            .Where(row => nameById.ContainsKey(row.AccessibleCompanyId))
            .GroupBy(row => row.MenuId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(row => nameById[row.AccessibleCompanyId])
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList());
    }
}

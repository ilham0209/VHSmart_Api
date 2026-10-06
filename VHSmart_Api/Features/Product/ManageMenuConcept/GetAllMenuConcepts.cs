using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Product.ManageMenuConcept;

// Manage Menu Concept list (spec 9.3, [CONFIRMED] columns): Action is client side, Menu
// Concept and Description are the form fields, Company Name is the owner of the row, and Menu
// is the concept's "List of Menu" - the menus currently linked with Mapping Status ACTIVE. An
// INACTIVE pair stays on the concept's detail table but is not one of its menus, so it is not
// listed here. Modified Date is the standard audit column this codebase adds to every list.
// Visibility is the plain tenant filter of CodingRules 7.1 (there is no "Accessible For" table
// for concepts), so a Switch Company = ALL caller sees every company's concepts.
// The two joined columns are filled AFTER paging: the search and the sort touch only scalar
// columns (RM-02 stance) and a collection can never decide the row order.
public record GetAllMenuConceptsQuery : IRequest<DataGridResponse<GetAllMenuConceptsResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllMenuConceptsResponse(
    Guid Id,
    string? Name,
    string? Description,
    string? CompanyName,
    IReadOnlyList<string> Menus,
    DateTime? ModifiedDate);

// The scalar projection the grid pages over. The two joined columns are NOT here: they are
// filled after paging (RM-02 stance), so a collection never takes part in the SQL, in the
// search or in the sort.
internal record MenuConceptListRow(
    Guid Id,
    string? Name,
    string? Description,
    Guid CompanyId,
    DateTime? ModifiedDate);

public class GetAllMenuConceptsHandler(VHSmartDbContext db)
    : IRequestHandler<GetAllMenuConceptsQuery, DataGridResponse<GetAllMenuConceptsResponse>>
{
    public async Task<DataGridResponse<GetAllMenuConceptsResponse>> Handle(
        GetAllMenuConceptsQuery request,
        CancellationToken ct)
    {
        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(GetAllMenuConceptsResponse.Name)
            : request.Request.SortBy;

        var page = await db.MenuConcepts
            .AsNoTracking()
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(MenuConceptEntity.Name),
                nameof(MenuConceptEntity.Description))
            .Select(row => new MenuConceptListRow(
                row.Id,
                row.Name,
                row.Description,
                row.CompanyId,
                row.SysDateModified))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);

        Guid[] conceptIds = [.. page.Data.Select(row => row.Id)];
        var companyNames = await MenuConceptListData.LoadCompanyNamesAsync(
            db, page.Data.Select(row => row.CompanyId).Distinct().ToList(), ct);
        var menus = conceptIds.Length == 0
            ? new Dictionary<Guid, IReadOnlyList<string>>()
            : await MenuConceptListData.LoadMenuNamesAsync(db, conceptIds, ct);

        return new DataGridResponse<GetAllMenuConceptsResponse>
        {
            Data = [.. page.Data.Select(row => new GetAllMenuConceptsResponse(
                row.Id,
                row.Name,
                row.Description,
                companyNames.GetValueOrDefault(row.CompanyId),
                menus.GetValueOrDefault(row.Id) ?? Array.Empty<string>(),
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

// The two joined columns of the list: the owner company's name and the names in the concept's
// "List of Menu". Both are read scoped to the page of ids the tenant filter has already
// approved - IgnoreQueryFilters only lifts the TENANT filter of the referenced row, the
// soft-delete flag is checked by hand, and no id reaches a query a visible row did not name.
internal static class MenuConceptListData
{
    public static async Task<IReadOnlyDictionary<Guid, string?>> LoadCompanyNamesAsync(
        VHSmartDbContext db,
        IReadOnlyCollection<Guid> companyIds,
        CancellationToken ct)
    {
        if (companyIds.Count == 0)
            return new Dictionary<Guid, string?>();

        var companies = await db.Companies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => companyIds.Contains(row.Id) && !row.IsDeleted)
            .Select(row => new { row.Id, Name = (string?)row.Name })
            .ToListAsync(ct);

        return companies.ToDictionary(row => row.Id, row => row.Name);
    }

    // The "Menu" column: menu names of the concept's ACTIVE links for one page, loaded after
    // paging. A menu that has since been deleted drops out (MenuConceptData.LoadMenuInfoAsync).
    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> LoadMenuNamesAsync(
        VHSmartDbContext db,
        IReadOnlyCollection<Guid> conceptIds,
        CancellationToken ct)
    {
        var links = await db.MenuConceptMenus
            .AsNoTracking()
            .Where(row => conceptIds.Contains(row.MenuConceptId)
                && row.MappingStatus == MenuConceptMenuMappingStatus.Active)
            .Select(row => new { row.MenuConceptId, row.MenuId })
            .ToListAsync(ct);

        var menuInfo = await MenuConceptData.LoadMenuInfoAsync(
            db, links.Select(row => row.MenuId).Distinct().ToList(), ct);

        return links
            .Where(link => menuInfo.ContainsKey(link.MenuId))
            .GroupBy(link => link.MenuConceptId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(link => menuInfo[link.MenuId].Name ?? string.Empty)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList());
    }
}

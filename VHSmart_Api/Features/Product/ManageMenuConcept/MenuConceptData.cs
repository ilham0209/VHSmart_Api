using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Product.ManageMenuConcept;

// The display data of one linked menu: its name and the "Menu Category" name behind it.
internal record MenuConceptMenuInfo(string? Name, string? Category);

// One place where a command becomes a row, so Create and Update can never disagree about the
// mapping (spec 9.3 modal "Manage Menu Concept"). CompanyId is never part of it: the form's
// "For Company*" is the caller's own company taken from the JWT on create, and an edit never
// moves a concept to another company (CodingRules 8.1).
internal static class MenuConceptData
{
    public static MenuConceptEntity Apply(
        MenuConceptEntity entity,
        CreateMenuConceptCommand request) =>
        Assign(entity, request.Name, request.Description);

    public static MenuConceptEntity Apply(
        MenuConceptEntity entity,
        UpdateMenuConceptCommand request) =>
        Assign(entity, request.Name, request.Description);

    private static MenuConceptEntity Assign(
        MenuConceptEntity entity,
        string? name,
        string? description)
    {
        entity.Name = name;
        entity.Description = description;

        return entity;
    }

    // The owner company behind the list's "Company Name" column. Read IgnoreQueryFilters but
    // always scoped to the one id the row names, and the soft-delete flag is checked by hand:
    // a company that has gone renders an empty cell instead of losing the concept row (the
    // RawMaterialResponseData stance).
    public static async Task<string?> LoadCompanyNameAsync(
        VHSmartDbContext db,
        Guid companyId,
        CancellationToken ct) =>
        await db.Companies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => row.Id == companyId && !row.IsDeleted)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct);

    // The name and category of the menus the link rows point at, for one page of menu ids.
    // The menu may belong to another company (a menu shared into the concept, CodingRules
    // 7.3) and its category sits in that company's reference data, so both are read
    // IgnoreQueryFilters but always scoped to the ids the link rows already named, with
    // !IsDeleted checked by hand - a menu deleted AFTER it was linked drops out of the table
    // instead of rendering an orphan (PD-04's DeleteMenu leaves the link rows behind).
    public static async Task<IReadOnlyDictionary<Guid, MenuConceptMenuInfo>> LoadMenuInfoAsync(
        VHSmartDbContext db,
        IReadOnlyCollection<Guid> menuIds,
        CancellationToken ct)
    {
        if (menuIds.Count == 0)
            return new Dictionary<Guid, MenuConceptMenuInfo>();

        var menus = await db.Menus
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => menuIds.Contains(row.Id) && !row.IsDeleted)
            .Select(row => new { row.Id, row.Name, row.CategoryId })
            .ToListAsync(ct);

        var categoryIds = menus.Select(row => row.CategoryId).Distinct().ToList();
        var categories = await db.GeneralData
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => categoryIds.Contains(row.Id) && !row.IsDeleted)
            .Select(row => new { row.Id, row.Name })
            .ToListAsync(ct);
        var categoryById = categories.ToDictionary(row => row.Id, row => row.Name);

        return menus.ToDictionary(
            row => row.Id,
            row => new MenuConceptMenuInfo(row.Name, categoryById.GetValueOrDefault(row.CategoryId)));
    }
}

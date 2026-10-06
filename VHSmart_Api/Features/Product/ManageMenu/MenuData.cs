using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageMenu;

// The Group/Category literal of the "Manage Menu" form dropdown (spec 5.1 reference data is
// per company; GeneralDataCatalog carries the pair under the COMPANY group). One definition
// for the options endpoint and both validators (D-05: no repeated business strings).
internal static class MenuGeneralDataCategory
{
    public const string MenuCategory = "Menu Category";
}

// One place where a command becomes a row, so Create and Update can never disagree about the
// mapping (spec 9.2 modal "Add New Menu"). Status is never assigned here: a menu is ACTIVE
// from creation (the modal has no Status field) and nothing in this task changes it.
internal static class MenuData
{
    public static MenuEntity Apply(
        MenuEntity entity,
        CreateMenuCommand request) =>
        Assign(entity, request.Name, request.CategoryId, request.StartDate, request.EndDate, request.Description);

    public static MenuEntity Apply(
        MenuEntity entity,
        UpdateMenuCommand request) =>
        Assign(entity, request.Name, request.CategoryId, request.StartDate, request.EndDate, request.Description);

    private static MenuEntity Assign(
        MenuEntity entity,
        string? name,
        Guid? categoryId,
        DateTime? startDate,
        DateTime? endDate,
        string? description)
    {
        entity.Name = name;
        entity.CategoryId = categoryId!.Value;
        entity.StartDate = startDate;
        entity.EndDate = endDate;
        entity.Description = description;

        return entity;
    }

    // The visibility filter (CodingRules 7.3) also shows menus shared WITH the caller, but
    // sharing is read-only: only the owner company may edit or delete the menu - plus a
    // Switch Company = ALL token, which the tenant filter already trusts everywhere else.
    // Anything else answers 404 so existence is never revealed (CodingRules 9).
    public static void EnsureOwner(MenuEntity entity, ICurrentUser user)
    {
        var isOwner = entity.CompanyId == user.CompanyId;
        var isViewAll = user.IsPlatformAdmin || user.ViewAllCompanies;

        if (!isOwner && !isViewAll)
            throw new NotFoundException("Menu not found.");
    }

    // The category NAME behind a menu the caller is already allowed to read: AdmGeneralData is
    // a tenant table, so a menu shared with the caller would otherwise render an empty Category
    // cell (the row belongs to the owner's reference data). Read IgnoreQueryFilters but always
    // scoped to the one id the menu itself names.
    public static async Task<string?> LoadCategoryNameAsync(
        VHSmartDbContext db,
        Guid categoryId,
        CancellationToken ct) =>
        await db.GeneralData
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => row.Id == categoryId && !row.IsDeleted)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct);
}

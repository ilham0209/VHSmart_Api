using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Product.ManageMenu;

// The view/edit form of one menu (spec 9.2 modal "Add New Menu"): every field of the form plus
// the "List of Company" table the Accessible For picker fills and the attached
// "List of Raw Materials" table. Shared by Get / Create / Update like RawMaterialResponse is.
// The visibility filter (CodingRules 7.3) makes another company's menu answer 404 unless it
// was shared with the caller - and a shared menu reads whole (see the loaders).
public record GetMenuByIdQuery(Guid Id) : IRequest<MenuResponse>;

public record MenuAccessibleCompanyResponse(Guid CompanyId, string CompanyName);

public record MenuResponse(
    Guid Id,
    string? Name,
    Guid CategoryId,
    string? Category,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Description,
    string Status,
    IReadOnlyList<MenuAccessibleCompanyResponse> AccessibleFor,
    IReadOnlyList<MenuIngredientResponse> RawMaterials,
    DateTime? ModifiedDate);

public class GetMenuByIdHandler(VHSmartDbContext db)
    : IRequestHandler<GetMenuByIdQuery, MenuResponse>
{
    public async Task<MenuResponse> Handle(GetMenuByIdQuery request, CancellationToken ct)
    {
        var entity = await db.Menus
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Menu not found.");

        return await MenuResponseData.From(db, entity, ct);
    }
}

// The category name, the company list and the ingredient list are read here rather than
// through a navigation so the detail, create and update answers are built by exactly one code
// path (the RawMaterialResponseData / ProductResponseData reasoning).
internal static class MenuResponseData
{
    public static async Task<MenuResponse> From(
        VHSmartDbContext db,
        MenuEntity entity,
        CancellationToken ct)
    {
        var category = await MenuData.LoadCategoryNameAsync(db, entity.CategoryId, ct);

        // A company row that has gone (soft deleted) still keeps its id on the sharing row; it
        // renders as an empty cell instead of silently losing the entry (RawMaterialResponseData).
        var accessibleFor = await db.MenuAccessibleCompanies
            .AsNoTracking()
            .Where(row => row.MenuId == entity.Id)
            .Select(row => new
            {
                row.AccessibleCompanyId,
                CompanyName = db.Companies
                    .Where(company => company.Id == row.AccessibleCompanyId)
                    .Select(company => company.Name)
                    .FirstOrDefault() ?? string.Empty
            })
            .ToListAsync(ct);

        var rawMaterials = await MenuIngredientListLoader.LoadAsync(db, entity.Id, ct);

        return new MenuResponse(
            entity.Id,
            entity.Name,
            entity.CategoryId,
            category,
            entity.StartDate,
            entity.EndDate,
            entity.Description,
            entity.Status,
            [.. accessibleFor
                .OrderBy(row => row.CompanyName, StringComparer.Ordinal)
                .Select(row => new MenuAccessibleCompanyResponse(
                    row.AccessibleCompanyId, row.CompanyName))],
            rawMaterials,
            entity.SysDateModified);
    }
}

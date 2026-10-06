using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Product.ManageMenuConcept;

// The view/edit form of one concept (spec 9.3 modal "Manage Menu Concept"): every field of the
// form plus its "List of Menu" table (Action is client side, Menu Name, Menu Category and
// Mapping Status are what the Link button works on). Shared by Get / Create / Update like
// MenuResponse is. The tenant filter makes another company's concept answer 404.
public record GetMenuConceptByIdQuery(Guid Id) : IRequest<MenuConceptResponse>;

// One row of the concept's "List of Menu": the menu as the caller may see it (7.3) plus the
// mapping status of THIS pair - ACTIVE when the concept lists the menu, INACTIVE when it was
// unlinked. A pair whose menu has been deleted in the meantime is not a row at all.
public record MenuConceptMenuResponse(
    Guid MenuId,
    string? MenuName,
    string? MenuCategory,
    string MappingStatus);

public record MenuConceptResponse(
    Guid Id,
    string? Name,
    Guid CompanyId,
    string? CompanyName,
    string? Description,
    IReadOnlyList<MenuConceptMenuResponse> Menus,
    DateTime? ModifiedDate);

public class GetMenuConceptByIdHandler(VHSmartDbContext db)
    : IRequestHandler<GetMenuConceptByIdQuery, MenuConceptResponse>
{
    public async Task<MenuConceptResponse> Handle(GetMenuConceptByIdQuery request, CancellationToken ct)
    {
        var entity = await db.MenuConcepts
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Menu concept not found.");

        return await MenuConceptResponseData.From(db, entity, ct);
    }
}

// The company name, the linked menus and their categories are read here rather than through a
// navigation so the detail, create and update answers are built by exactly one code path (the
// MenuResponseData / ProductResponseData reasoning).
internal static class MenuConceptResponseData
{
    public static async Task<MenuConceptResponse> From(
        VHSmartDbContext db,
        MenuConceptEntity entity,
        CancellationToken ct)
    {
        var companyName = await MenuConceptData.LoadCompanyNameAsync(db, entity.CompanyId, ct);

        // The concept itself is already approved (tenant filter + 404), and every link row
        // carries ITS company, so the plain filtered query sees exactly this concept's pairs.
        var links = await db.MenuConceptMenus
            .AsNoTracking()
            .Where(row => row.MenuConceptId == entity.Id)
            .Select(row => new { row.MenuId, row.MappingStatus })
            .ToListAsync(ct);

        var menuInfo = await MenuConceptData.LoadMenuInfoAsync(
            db, links.Select(row => row.MenuId).Distinct().ToList(), ct);

        var menus = links
            .Where(link => menuInfo.ContainsKey(link.MenuId))
            .Select(link => new MenuConceptMenuResponse(
                link.MenuId,
                menuInfo[link.MenuId].Name,
                menuInfo[link.MenuId].Category,
                link.MappingStatus))
            .OrderBy(row => row.MenuName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.MenuId)
            .ToList();

        return new MenuConceptResponse(
            entity.Id,
            entity.Name,
            entity.CompanyId,
            companyName,
            entity.Description,
            menus,
            entity.SysDateModified);
    }
}

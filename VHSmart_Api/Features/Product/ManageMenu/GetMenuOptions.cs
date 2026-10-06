using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageMenu;

// The dropdown sources of the "Manage Menu" form (spec 9.2): Menu Category comes from the
// caller's own COMPANY General Data (spec 5.1 reference data is per company; GeneralDataCatalog
// carries the pair), and the "Accessible For" picker's "+" dialog offers the company list - the
// same non-tenant-scoped stance the raw material options endpoint takes, because sharing means
// naming other companies. One endpoint behind the Product.ManageMenu View action so the screen
// works without the Admin keys (the CI-01 / P-01 / RM-02 reasoning).
public record GetMenuOptionsQuery : IRequest<MenuOptionsResponse>;

public record MenuOptionsResponse(
    IReadOnlyList<MenuOption> Categories,
    IReadOnlyList<MenuOption> Companies);

public record MenuOption(Guid Id, string Name);

public class GetMenuOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetMenuOptionsQuery, MenuOptionsResponse>
{
    public async Task<MenuOptionsResponse> Handle(
        GetMenuOptionsQuery request,
        CancellationToken ct)
    {
        var categories = await db.GeneralData
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.Group == GeneralDataGroup.COMPANY
                && row.Category == MenuGeneralDataCategory.MenuCategory)
            .OrderBy(row => row.Name)
            .Select(row => new MenuOption(row.Id, row.Name))
            .ToListAsync(ct);

        var companies = await db.Companies
            .AsNoTracking()
            .OrderBy(row => row.Name)
            .Select(row => new MenuOption(row.Id, row.Name))
            .ToListAsync(ct);

        return new MenuOptionsResponse(categories, companies);
    }
}

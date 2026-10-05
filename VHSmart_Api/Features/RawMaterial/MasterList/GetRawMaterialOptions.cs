using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.RawMaterial.MasterList;

// The dropdown sources of the "Manage Master Raw Material" form (spec 10.2): Ingredient Status
// and Ingredient Source come from the caller's own PRODUCT General Data (spec 5.1 reference data
// is per company, GeneralDataCatalog has both categories), Existing Manufacturer lists the rows
// the caller owns of RawManufacturerSuppliers (a manufacturer is picked, never typed - Database.md
// 8 keeps only the ManufacturerSupplierId FK), and the Accessible For picker offers the company
// list of the "+" dialog. One endpoint behind the RawMaterial.MasterList View action so the
// screen works without the Admin.GeneralData or Admin.Companies keys (same reasoning as the
// CI-01/P-01 options endpoints).
// The company list is intentionally NOT tenant-scoped: sharing means naming other companies, so
// it carries every live company ordered by name - only the id and name are exposed.
public record GetRawMaterialOptionsQuery : IRequest<RawMaterialOptionsResponse>;

public record RawMaterialOptionsResponse(
    IReadOnlyList<RawMaterialGeneralDataOption> IngredientStatuses,
    IReadOnlyList<RawMaterialGeneralDataOption> IngredientSources,
    IReadOnlyList<RawMaterialManufacturerOption> Manufacturers,
    IReadOnlyList<RawMaterialCompanyOption> Companies);

public record RawMaterialGeneralDataOption(Guid Id, string Name);

public record RawMaterialManufacturerOption(Guid Id, string Name);

public record RawMaterialCompanyOption(Guid Id, string Name);

public class GetRawMaterialOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetRawMaterialOptionsQuery, RawMaterialOptionsResponse>
{
    private const string IngredientStatusCategory = "Ingredient Status";

    private const string IngredientSourceCategory = "Ingredient Source";

    public async Task<RawMaterialOptionsResponse> Handle(
        GetRawMaterialOptionsQuery request,
        CancellationToken ct)
    {
        var ingredientStatuses = await db.GeneralData
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.Group == GeneralDataGroup.PRODUCT
                && row.Category == IngredientStatusCategory)
            .OrderBy(row => row.Name)
            .Select(row => new RawMaterialGeneralDataOption(row.Id, row.Name))
            .ToListAsync(ct);

        var ingredientSources = await db.GeneralData
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.Group == GeneralDataGroup.PRODUCT
                && row.Category == IngredientSourceCategory)
            .OrderBy(row => row.Name)
            .Select(row => new RawMaterialGeneralDataOption(row.Id, row.Name))
            .ToListAsync(ct);

        // "Existing Manufacturer?" - a row that carries no manufacturer half (supplier-only)
        // cannot be picked as a manufacturer, so it is left out.
        var manufacturers = await db.ManufacturerSuppliers
            .AsNoTracking()
            .Where(row => row.ManufacturerName != null)
            .OrderBy(row => row.ManufacturerName)
            .Select(row => new RawMaterialManufacturerOption(row.Id, row.ManufacturerName!))
            .ToListAsync(ct);

        var companies = await db.Companies
            .AsNoTracking()
            .OrderBy(row => row.Name)
            .Select(row => new RawMaterialCompanyOption(row.Id, row.Name))
            .ToListAsync(ct);

        return new RawMaterialOptionsResponse(
            ingredientStatuses,
            ingredientSources,
            manufacturers,
            companies);
    }
}

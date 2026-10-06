using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageProduct;

// The Group/Category literals of the "Manage Product" form dropdowns (spec 5.1 reference data
// is per company, GeneralDataCatalog carries the three pairs). One definition for the options
// endpoint and both validators (D-05: no repeated business strings).
internal static class ProductGeneralDataCategory
{
    public const string Brand = "Brand";

    public const string ProductCategory = "Product Category";

    public const string MarketingMethod = "Marketing Method";
}

// The dropdown sources of the "Manage Product" form (spec 9.1): Product Scheme comes from the
// seeded global AdmSchemes (12.1), Manufacturer lists the RawManufacturerSuppliers rows the
// caller may pick (a supplier-only row cannot name a product manufacturer - the same set the
// raw material form offers), and Brand / Category / Marketing Method are the caller's own
// General Data rows (GeneralDataCatalog has the three categories). One endpoint behind the
// Product.ManageProduct View action so the screen works without the Admin keys (same
// reasoning as the raw material and premise options endpoints).
// The form's "For Company" dropdown is NOT served here: every screen in this codebase takes
// the company from the JWT only (CodingRules 8.1), so the client shows the caller's own
// company - flagged in the report.
public record GetProductOptionsQuery : IRequest<ProductOptionsResponse>;

public record ProductOptionsResponse(
    IReadOnlyList<ProductOption> Schemes,
    IReadOnlyList<ProductOption> Manufacturers,
    IReadOnlyList<ProductOption> Brands,
    IReadOnlyList<ProductOption> Categories,
    IReadOnlyList<ProductOption> MarketingMethods);

public record ProductOption(Guid Id, string Name);

public class GetProductOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetProductOptionsQuery, ProductOptionsResponse>
{
    public async Task<ProductOptionsResponse> Handle(
        GetProductOptionsQuery request,
        CancellationToken ct)
    {
        var schemes = await db.Schemes
            .AsNoTracking()
            .OrderBy(row => row.SortOrder)
            .Select(row => new ProductOption(row.Id, row.Name))
            .ToListAsync(ct);

        // A row with no manufacturer half is not a manufacturer (spec 9.1 "Manufacturer*").
        var manufacturers = await db.ManufacturerSuppliers
            .AsNoTracking()
            .Where(row => row.ManufacturerName != null)
            .OrderBy(row => row.ManufacturerName)
            .Select(row => new ProductOption(row.Id, row.ManufacturerName!))
            .ToListAsync(ct);

        var brands = await LoadGeneralDataAsync(
            db, user.CompanyId, GeneralDataGroup.COMPANY, ProductGeneralDataCategory.Brand, ct);

        var categories = await LoadGeneralDataAsync(
            db, user.CompanyId, GeneralDataGroup.PRODUCT, ProductGeneralDataCategory.ProductCategory, ct);

        var marketingMethods = await LoadGeneralDataAsync(
            db, user.CompanyId, GeneralDataGroup.PRODUCT, ProductGeneralDataCategory.MarketingMethod, ct);

        return new ProductOptionsResponse(schemes, manufacturers, brands, categories, marketingMethods);
    }

    private static async Task<IReadOnlyList<ProductOption>> LoadGeneralDataAsync(
        VHSmartDbContext db,
        Guid companyId,
        GeneralDataGroup group,
        string category,
        CancellationToken ct) =>
        await db.GeneralData
            .AsNoTracking()
            .Where(row => row.CompanyId == companyId
                && row.Group == group
                && row.Category == category)
            .OrderBy(row => row.Name)
            .Select(row => new ProductOption(row.Id, row.Name))
            .ToListAsync(ct);
}

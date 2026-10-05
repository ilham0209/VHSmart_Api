using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Product.ManageProduct;

// Manage Product list (spec 9.1 "LIST OF PRODUCT", [CONFIRMED] columns): Action is client
// side (only the Id is needed), Brand and Manufacturer are joined display names, Modified
// Date is the standard audit column. Two spec columns are missing on purpose: the "Company"
// column - every list in this codebase is tenant scoped and "Switch Company" re-issues the
// token (CodingRules 7.4), same stance as the premise and raw material lists - and the three
// DERIVED columns (Ingredient Link Status, Halal Status, Expiry). Database.md 9 derives them
// from PrdProductIngredients, which PD-02 creates (D-18 lands there), exactly like Halal
// Information in RM-02 arriving with RM-03. Search covers the values a user knows the
// product by (name, code, GTIN); default order is Name ascending (the spec states no order).
public record GetAllProductsQuery : IRequest<DataGridResponse<GetAllProductsResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllProductsResponse(
    Guid Id,
    string? Name,
    string? Brand,
    string? Manufacturer,
    DateTime? ModifiedDate);

public class GetAllProductsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllProductsQuery, DataGridResponse<GetAllProductsResponse>>
{
    public async Task<DataGridResponse<GetAllProductsResponse>> Handle(
        GetAllProductsQuery request,
        CancellationToken ct)
    {
        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(GetAllProductsResponse.Name)
            : request.Request.SortBy;

        var grid = await db.Products
            .AsNoTracking()
            // A Switch Company = ALL caller still gets the premise list's guard: their OWN
            // rows (PR-01 precedent) - the tenant filter alone would widen this.
            .Where(row => row.CompanyId == user.CompanyId)
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(ProductEntity.Name),
                nameof(ProductEntity.Code),
                nameof(ProductEntity.Gtin))
            .Select(row => new GetAllProductsResponse(
                row.Id,
                row.Name,
                db.GeneralData
                    .Where(data => data.Id == row.BrandId)
                    .Select(data => data.Name)
                    .FirstOrDefault(),
                // A supplier-only row has no manufacturer name; the product still names it,
                // so the supplier half renders instead of an empty cell.
                db.ManufacturerSuppliers
                    .Where(supplier => supplier.Id == row.ManufacturerSupplierId)
                    .Select(supplier => supplier.ManufacturerName ?? supplier.SupplierName)
                    .FirstOrDefault(),
                row.SysDateModified))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);

        return grid;
    }
}

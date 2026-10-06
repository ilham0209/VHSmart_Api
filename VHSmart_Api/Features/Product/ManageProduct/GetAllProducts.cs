using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Product.ManageProduct;

// Manage Product list (spec 9.1 "LIST OF PRODUCT", [CONFIRMED] columns): Action is client
// side (only the Id is needed), Brand and Manufacturer are joined display names, Modified
// Date is the standard audit column. One spec column is missing on purpose: the "Company"
// column - every list in this codebase is tenant scoped and "Switch Company" re-issues the
// token (CodingRules 7.4), same stance as the premise and raw material lists. The three
// DERIVED columns (Ingredient Link Status, Halal Status, Expiry) arrive with this task: they
// are computed from PrdProductIngredients + the raw materials' HALAL CERTIFICATE per D-18 and
// filled for the page AFTER paging, so they never take part in the search or the sort (the
// raw material list's halal column does the same). Search covers the values a user knows the
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
    string IngredientLinkStatus,
    HalalStatus HalalStatus,
    DateOnly? ExpiryDate,
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
                // The three derived columns (spec 9.1, D-18): placeholders here, real values
                // for THIS page after paging - a derived value can never be sorted or searched
                // in SQL, and the fill below overwrites every row it returns.
                string.Empty,
                HalalStatus.Expired,
                null,
                row.SysDateModified))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);

        var halalInformation = await ProductHalalInformation.LoadManyAsync(
            db, [.. grid.Data.Select(row => row.Id)], ct);
        grid.Data = [.. grid.Data.Select(row =>
        {
            var derived = halalInformation.GetValueOrDefault(row.Id)
                ?? ProductHalalInformation.NoIngredient;

            return row with
            {
                IngredientLinkStatus = derived.IsIngredientLinked
                    ? ProductIngredientLinkStatus.Linked
                    : ProductIngredientLinkStatus.Unlinked,
                HalalStatus = derived.HalalStatus,
                ExpiryDate = derived.ExpiryDate
            };
        })];

        return grid;
    }
}

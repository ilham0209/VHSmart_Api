using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Product.VerifyHalalProductUpdate;

// Verify Halal Product Update list (spec 9.4 "List of Products", [CONFIRMED] columns):
// Action is client side, Halal Application No. is the application reference number, Product
// Name and Brand are display values, Halal Expiry Date is the product's earliest linked-raw-
// material certificate expiry (the same D-18 derivation the Manage Product list uses), and
// Publish Status in Verify Halal is the stored column. D-28: build the local list only, no
// calls to the external Verify Halal system - so HalalApplicationNo stays null until the
// Halal Application tables (HA-02/HA-06) provide the join; the column shape matches the
// spec so the frontend can render it. The "View By" dropdown filters by product category
// (the company's PRODUCT / "Product Category" General Data rows). Search covers Product
// Name; default order is Product Name ascending (the spec states no order).
public record GetVerifyHalalProductsQuery : IRequest<DataGridResponse<GetVerifyHalalProductsResponse>>
{
    public DataGridRequest Request { get; set; } = new();

    public Guid? CategoryId { get; set; }
}

public record GetVerifyHalalProductsResponse(
    Guid Id,
    string? HalalApplicationNo,
    string? ProductName,
    string? Brand,
    DateOnly? HalalExpiryDate,
    string? PublishStatus);

public class GetVerifyHalalProductsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetVerifyHalalProductsQuery, DataGridResponse<GetVerifyHalalProductsResponse>>
{
    public async Task<DataGridResponse<GetVerifyHalalProductsResponse>> Handle(
        GetVerifyHalalProductsQuery request,
        CancellationToken ct)
    {
        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(GetVerifyHalalProductsResponse.ProductName)
            : request.Request.SortBy;

        var query = db.Products
            .AsNoTracking()
            // A Switch Company = ALL caller still gets their OWN rows (PR-01 precedent).
            .Where(row => row.CompanyId == user.CompanyId);

        if (request.CategoryId is { } categoryId)
            query = query.Where(row => row.CategoryId == categoryId);

        var grid = await query
            .ApplySearch(request.Request.SearchTerm, nameof(ProductEntity.Name))
            .Select(row => new GetVerifyHalalProductsResponse(
                row.Id,
                // D-28: no external Verify Halal call; the application number arrives with
                // the Halal Application tables (HA-02/HA-06) which do not exist yet.
                null,
                row.Name,
                db.GeneralData
                    .Where(data => data.Id == row.BrandId)
                    .Select(data => data.Name)
                    .FirstOrDefault(),
                // Placeholder; the real D-18 expiry is filled for THIS page after paging.
                null,
                row.VerifyHalalPublishStatus))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);

        var halalInformation = await ProductHalalInformation.LoadManyAsync(
            db, [.. grid.Data.Select(row => row.Id)], ct);
        grid.Data = [.. grid.Data.Select(row =>
        {
            var derived = halalInformation.GetValueOrDefault(row.Id);

            return row with
            {
                HalalExpiryDate = derived?.ExpiryDate
            };
        })];

        return grid;
    }
}

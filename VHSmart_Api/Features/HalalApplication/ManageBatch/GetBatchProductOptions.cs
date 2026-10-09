using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.HalalApplication.ManageBatch;

// The source behind the edit modal's product picker (spec 12.2 flow 5 "click Link on the
// products"): the caller's own products minus every row this batch already has, ACTIVE and
// INACTIVE alike - an INACTIVE pair is not "available to add", it sits on the modal's own
// product list with its link icon and LinkBatchProduct flips it back (the ingredient tab's
// options reasoning). Brand is a stored join; Link Ingredient Status and Halal Status are
// D-18 DERIVED values filled AFTER paging - they never take part in the search or the sort
// (the RM-02 / PD-06 stance) - and the D-18 block itself is enforced by LinkBatchProduct, so
// an expired row shown here still cannot be linked (Q10 default: block).
public record GetBatchProductOptionsQuery(Guid BatchId)
    : IRequest<DataGridResponse<BatchProductOptionResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record BatchProductOptionResponse(
    Guid Id,
    string? Name,
    string Brand,
    string LinkIngredientStatus,
    HalalStatus HalalStatus);

public class GetBatchProductOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetBatchProductOptionsQuery, DataGridResponse<BatchProductOptionResponse>>
{
    public async Task<DataGridResponse<BatchProductOptionResponse>> Handle(
        GetBatchProductOptionsQuery request,
        CancellationToken ct)
    {
        var batchExists = await db.Batches
            .AsNoTracking()
            .AnyAsync(
                row => row.Id == request.BatchId && row.CompanyId == user.CompanyId, ct);
        if (!batchExists)
            throw new NotFoundException("Batch not found.");

        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(BatchProductOptionResponse.Name)
            : request.Request.SortBy;

        var linkedProductIds = db.BatchProducts
            .AsNoTracking()
            .Where(row => row.BatchId == request.BatchId)
            .Select(row => row.ProductId);

        var grid = await db.Products
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && !linkedProductIds.Contains(row.Id))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(ProductEntity.Name),
                nameof(ProductEntity.Code),
                nameof(ProductEntity.Gtin))
            .Select(row => new BatchProductOptionResponse(
                row.Id,
                row.Name,
                db.GeneralData
                    .Where(data => data.Id == row.BrandId)
                    .Select(data => data.Name)
                    .FirstOrDefault() ?? string.Empty,
                string.Empty,
                HalalStatus.Valid))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);

        // D-18 over this page only, in one query set (the product list's own derivation).
        List<Guid> pageIds = [.. grid.Data.Select(row => row.Id)];
        if (pageIds.Count == 0)
            return grid;

        var halalInfo = await ProductHalalInformation.LoadManyAsync(db, pageIds, ct);
        grid.Data = [.. grid.Data.Select(row =>
        {
            var halal = halalInfo.GetValueOrDefault(row.Id);
            return row with
            {
                LinkIngredientStatus = halal?.IsIngredientLinked == true
                    ? ProductIngredientLinkStatus.Linked
                    : ProductIngredientLinkStatus.Unlinked,
                HalalStatus = halal?.HalalStatus ?? HalalStatus.Expired
            };
        })];

        return grid;
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.ManageBatch;

// The view/edit form of one batch (spec 12.2 edit modal): the form fields plus the linked
// "List of Products" (product scheme) or the premises behind "Associate Premise" (Food
// Premise scheme). Product rows carry the Link Ingredient Status and Mapping Status the spec
// shows; premise rows carry their names. Unknown or foreign batch -> 404 (never 403). The
// scheme's IsFoodPremise flag decides which child list is filled - the other stays empty.
// Shared by Get / Create / Update like ProductResponse is.
public record GetBatchByIdQuery(Guid Id) : IRequest<BatchResponse>;

public record BatchResponse(
    Guid Id,
    Guid SchemeId,
    bool IsFoodPremiseScheme,
    string Name,
    string? CbReferenceNo,
    DateTime? SubmissionPlannedDate,
    Guid? BrandId,
    Guid? ManufacturerSupplierId,
    string? Description,
    IReadOnlyList<BatchProductResponse> Products,
    IReadOnlyList<BatchPremiseResponse> Premises,
    DateTime CreatedDate);

public record BatchProductResponse(
    Guid BatchProductId,
    Guid ProductId,
    string ProductName,
    string Brand,
    string LinkIngredientStatus,
    string MappingStatus);

public record BatchPremiseResponse(
    Guid BatchPremiseId,
    Guid PremiseId,
    string PremiseName);

public class GetBatchByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetBatchByIdQuery, BatchResponse>
{
    public async Task<BatchResponse> Handle(
        GetBatchByIdQuery request,
        CancellationToken ct)
    {
        // The explicit CompanyId match keeps a Switch Company = ALL caller on their own rows
        // (same guard as every other detail of this codebase).
        var entity = await db.Batches
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Batch not found.");

        return await BatchResponseData.From(db, entity, ct);
    }
}

// The detail is read here rather than through navigations so get, create and update answer
// from exactly one code path (mirrors ProductResponseData).
internal static class BatchResponseData
{
    public static async Task<BatchResponse> From(
        VHSmartDbContext db,
        BatchEntity entity,
        CancellationToken ct)
    {
        var isFoodPremise = await BatchData.IsFoodPremiseSchemeAsync(db, entity.SchemeId, ct);

        IReadOnlyList<BatchProductResponse> products = [];
        IReadOnlyList<BatchPremiseResponse> premises = [];

        if (isFoodPremise)
        {
            premises = await (
                    from link in db.BatchPremises.AsNoTracking()
                    where link.BatchId == entity.Id
                    join premise in db.Premises.AsNoTracking()
                        on link.PremiseId equals premise.Id
                    orderby premise.Name
                    select new BatchPremiseResponse(link.Id, premise.Id, premise.Name))
                .ToListAsync(ct);
        }
        else
        {
            var links = await db.BatchProducts.AsNoTracking()
                .Where(row => row.BatchId == entity.Id)
                .ToListAsync(ct);

            // Name / brand / D-18 status are joined in two queries for the whole list, never
            // one per link, and a product deleted after linking simply drops out.
            var productIds = links.Select(row => row.ProductId).ToList();
            var productInfo = await db.Products.AsNoTracking()
                .Where(row => productIds.Contains(row.Id))
                .Select(row => new
                {
                    row.Id,
                    row.Name,
                    BrandName = db.GeneralData
                        .Where(data => data.Id == row.BrandId)
                        .Select(data => data.Name)
                        .FirstOrDefault()
                })
                .ToDictionaryAsync(row => row.Id, ct);
            var halalInfo = await ProductHalalInformation.LoadManyAsync(db, productIds, ct);

            products = [.. links
                .Where(link => productInfo.ContainsKey(link.ProductId))
                .Select(link =>
                {
                    var product = productInfo[link.ProductId];
                    var halal = halalInfo.GetValueOrDefault(link.ProductId);
                    return new BatchProductResponse(
                        link.Id,
                        link.ProductId,
                        product.Name ?? string.Empty,
                        product.BrandName ?? string.Empty,
                        halal?.IsIngredientLinked == true
                            ? ProductIngredientLinkStatus.Linked
                            : ProductIngredientLinkStatus.Unlinked,
                        link.MappingStatus);
                })
                .OrderBy(row => row.ProductName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.ProductId)];
        }

        return new BatchResponse(
            entity.Id,
            entity.SchemeId,
            isFoodPremise,
            entity.Name,
            entity.CbReferenceNo,
            entity.SubmissionPlannedDate,
            entity.BrandId,
            entity.ManufacturerSupplierId,
            entity.Description,
            products,
            premises,
            entity.SysDateCreated);
    }
}

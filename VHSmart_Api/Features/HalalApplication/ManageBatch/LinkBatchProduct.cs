using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.ManageBatch;

// The Link half of the edit modal's product Action icon (spec 12.2 flow 5): one product joins
// one batch. Database.md 10 says link/unlink toggles MappingStatus (the row is kept, exactly
// like PrdProductIngredients), so an existing INACTIVE row for the pair is flipped back to
// ACTIVE instead of inserted twice - the filtered unique index (BatchId, ProductId) then
// always sees one live row. Three gates before the write: the batch must be the caller's own
// row and NOT a Food Premise batch (Database.md 10: premises + brand for that scheme), the
// product must be the caller's own row, and D-18 must find it Valid - a product with a
// missing / non-halal / expired raw material certificate cannot join a batch (owner rule
// "raw materials must be halal", spec 18 #14; the D-18 default blocks, Q10).
public record LinkBatchProductCommand(Guid BatchId, Guid ProductId)
    : IRequest<BatchProductResponse>;

public class LinkBatchProductValidator : AbstractValidator<LinkBatchProductCommand>
{
    public LinkBatchProductValidator()
    {
        RuleFor(x => x.ProductId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Product is required.");
    }
}

public class LinkBatchProductHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<LinkBatchProductCommand, BatchProductResponse>
{
    public async Task<BatchProductResponse> Handle(
        LinkBatchProductCommand request,
        CancellationToken ct)
    {
        var batch = await db.Batches
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.BatchId && row.CompanyId == user.CompanyId, ct);
        if (batch is null)
            throw new NotFoundException("Batch not found.");

        if (await BatchData.IsFoodPremiseSchemeAsync(db, batch.SchemeId, ct))
            throw new BusinessRuleException("This batch does not accept products.");

        var productExists = await db.Products
            .AsNoTracking()
            .AnyAsync(
                row => row.Id == request.ProductId && row.CompanyId == user.CompanyId, ct);
        if (!productExists)
            throw new NotFoundException("Product not found.");

        // D-18 through the product screen's own derivation, so both menus answer the same
        // Valid / Expired for the same certificate. Unlinked (no ingredient at all) is not
        // Valid either - D-18 only calls a product Valid from >= 1 linked raw material.
        var halalInfo = await ProductHalalInformation.LoadManyAsync(
            db, [request.ProductId], ct);
        var product = halalInfo.GetValueOrDefault(request.ProductId);
        if (product is null || !product.IsIngredientLinked)
            throw new BusinessRuleException("Product has no linked ingredient.");
        if (product.HalalStatus != HalalStatus.Valid)
            throw new BusinessRuleException(
                "Product has raw materials that are not halal or have expired.");

        var entity = await db.BatchProducts.FirstOrDefaultAsync(
            row => row.BatchId == request.BatchId && row.ProductId == request.ProductId,
            ct);

        if (entity is null)
        {
            entity = new BatchProductEntity
            {
                // The link belongs to the company that owns the batch (JWT only).
                CompanyId = user.CompanyId,
                BatchId = request.BatchId,
                ProductId = request.ProductId,
                MappingStatus = BatchProductMappingStatus.Active
            };
            db.BatchProducts.Add(entity);
        }
        else
        {
            entity.MappingStatus = BatchProductMappingStatus.Active;
        }

        await db.SaveChangesAsync(ct);

        // The edit modal's own table answers the row, so a link and a GET can never disagree
        // about what the list shows (the ProductIngredientListLoader stance).
        return await BatchProductRowData.LoadAsync(db, entity, ct);
    }
}

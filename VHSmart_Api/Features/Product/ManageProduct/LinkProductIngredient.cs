using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageProduct;

// "+ Add New Ingredient" of the Manage Ingredient Information tab (spec 9.1): links one raw
// material to the product. Database.md 9 says link/unlink toggles MappingStatus, so an existing
// UNLINKED row for the same pair is flipped back to ACTIVE instead of inserted twice - the
// filtered unique index (ProductId, RawMaterialId) then always sees one live row. The raw
// material must be one the caller can see (the 7.3 filter answers 404 for anything else), and
// the tab's >= 1 brand rule applies (spec 6.3 / 9.1).
public record LinkProductIngredientCommand(Guid ProductId, Guid? RawMaterialId)
    : IRequest<ProductIngredientResponse>;

public class LinkProductIngredientValidator : AbstractValidator<LinkProductIngredientCommand>
{
    public LinkProductIngredientValidator()
    {
        RuleFor(x => x.RawMaterialId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Raw material is required.");
    }
}

public class LinkProductIngredientHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<LinkProductIngredientCommand, ProductIngredientResponse>
{
    public async Task<ProductIngredientResponse> Handle(
        LinkProductIngredientCommand request,
        CancellationToken ct)
    {
        var productExists = await db.Products
            .AsNoTracking()
            .AnyAsync(
                row => row.Id == request.ProductId && row.CompanyId == user.CompanyId, ct);
        if (!productExists)
            throw new NotFoundException("Product not found.");

        await ProductIngredientData.EnsureBrandLinkedAsync(db, user, ct);

        // The validator rejects a missing value; a direct call without one lands here and gets
        // the same 404 an unknown id gets, instead of dereferencing null.
        if (request.RawMaterialId is not Guid rawMaterialId
            || !await db.RawMaterials.AsNoTracking()
                .AnyAsync(row => row.Id == rawMaterialId, ct))
            throw new NotFoundException("Raw material not found.");

        var entity = await db.ProductIngredients.FirstOrDefaultAsync(
            row => row.ProductId == request.ProductId
                && row.RawMaterialId == rawMaterialId,
            ct);

        if (entity is null)
        {
            entity = new ProductIngredientEntity
            {
                // The link belongs to the company that owns the product (JWT only), even when
                // the raw material was shared with it by another company.
                CompanyId = user.CompanyId,
                ProductId = request.ProductId,
                RawMaterialId = rawMaterialId,
                MappingStatus = ProductIngredientMappingStatus.Active
            };
            db.ProductIngredients.Add(entity);
        }
        else
        {
            entity.MappingStatus = ProductIngredientMappingStatus.Active;
        }

        await db.SaveChangesAsync(ct);

        // The tab's own loader answers the row, so a link and a GET can never disagree about
        // what the table shows.
        var rows = await ProductIngredientListLoader.LoadAsync(db, user, request.ProductId, ct);
        return rows.First(row => row.Id == entity.Id);
    }
}
